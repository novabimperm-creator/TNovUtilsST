using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TNovCommon;

namespace TNovUtilsST
{
    /// <summary>
    /// Заполняет N_Код материала у несущих элементов КЖ по материалу типа
    /// (замена Dynamo-скрипта "N_Код материала (КЖ).dyn").
    /// Код бетона = 1 000 000 000 + B·1 000 000 + W·1000 + F.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MaterialCodesST : IExternalCommand
    {
        #region Параметры
        const string CodeParamName = "N_Код материала";
        const string TMaterialParamName = "Т Материал";
        const int ThermoInsertPPSCode = 303020000;   //термовкладыш ППС
        const int ThermoInsertGEOCode = 302000001;   //термовкладыш ГЕО (прочие)

        static readonly BuiltInCategory[] Categories =
        {
            BuiltInCategory.OST_StructuralColumns,  //Несущие колонны
            BuiltInCategory.OST_Walls,              //Стены
            BuiltInCategory.OST_GenericModel,       //Обобщенные модели
            BuiltInCategory.OST_Floors,             //Перекрытия
            BuiltInCategory.OST_Stairs              //Лестницы
        };

        static readonly Regex ClassRegex = new Regex(@"^[ВB](\d+(?:[.,]\d+)?)$", RegexOptions.IgnoreCase);
        static readonly Regex WaterRegex = new Regex(@"^W(\d+)$", RegexOptions.IgnoreCase);
        static readonly Regex FrostRegex = new Regex(@"^[FФ](\d+)$", RegexOptions.IgnoreCase);
        #endregion

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Код материала КЖ";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion); if (config == null) return Result.Failed;

            #region Настройки логов
            // создание log - файла
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }
            #endregion

            #region Сбор элементов
            List<Element> items = new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(Categories.ToList()))
                .WhereElementIsNotElementType()
                .ToList();

            Logger.Log($"Найдено элементов: {items.Count}", 1);

            if (items.Count == 0)
            {
                new InfoWindow280("В модели не найдены несущие колонны, стены, перекрытия, лестницы и обобщенные модели.").ShowDialog();
                Logger.Log("Элементы не найдены. Завершение работы.", 5);
                return Result.Succeeded;
            }
            #endregion

            #region Основной код
            int setConcrete = 0, setPPS = 0, setGEO = 0, skippedOpenings = 0;
            List<string> noMaterial = new List<string>();
            Dictionary<string, int> badMaterials = new Dictionary<string, int>();
            List<string> noParam = new List<string>();
            Dictionary<ElementId, int?> materialCodeCache = new Dictionary<ElementId, int?>();

            using (Transaction trans = new Transaction(doc, "TNov - Код материала КЖ"))
            {
                trans.Start();

                foreach (Element e in items)
                {
                    Element type = doc.GetElement(e.GetTypeId());
                    string name = GetFilterName(e, type);

                    if (Contains(name, "Отверстие")) { skippedOpenings++; continue; }

                    int code;
                    if (Contains(name, "Термовкладыш"))
                    {
                        // термовкладыши кодируются только среди обобщенных моделей
                        if (RevitApiCompat.ElementIdIntValue(e.Category.Id) != (int)BuiltInCategory.OST_GenericModel) continue;
                        bool pps = Contains(name, "ППС");
                        code = pps ? ThermoInsertPPSCode : ThermoInsertGEOCode;
                        if (TrySetCode(e, code)) { if (pps) setPPS++; else setGEO++; }
                        else noParam.Add($"{e.Id} {name}");
                        continue;
                    }

                    Material material = GetMaterial(doc, e, type);
                    if (material == null) { noMaterial.Add($"{e.Id} {name}"); continue; }

                    if (!materialCodeCache.TryGetValue(material.Id, out int? cached))
                    {
                        cached = TryParseCode(material.Name, out int parsed) ? parsed : (int?)null;
                        materialCodeCache[material.Id] = cached;
                    }
                    if (cached == null)
                    {
                        badMaterials.TryGetValue(material.Name, out int n);
                        badMaterials[material.Name] = n + 1;
                        continue;
                    }

                    if (TrySetCode(e, cached.Value)) setConcrete++;
                    else noParam.Add($"{e.Id} {name}");
                }

                trans.Commit();
            }
            #endregion

            #region Отчёт
            string head = $"Заполнен {CodeParamName}:\n" +
                          $"бетонные элементы — {setConcrete}\n" +
                          $"термовкладыши ППС — {setPPS}, ГЕО — {setGEO}\n" +
                          $"пропущено отверстий — {skippedOpenings}";
            Logger.Log(head.Replace("\n", "; "), 1);

            List<string> problems = new List<string>();
            if (badMaterials.Count > 0)
            {
                problems.Add("Не распознана маркировка бетона (нужны класс В, W и F), элементов:");
                problems.AddRange(badMaterials.OrderBy(kv => kv.Key).Select(kv => $"  {kv.Key} — {kv.Value}"));
            }
            if (noMaterial.Count > 0)
            {
                problems.Add($"Не задан материал в типе ({TMaterialParamName} / Материал несущих конструкций):");
                problems.AddRange(noMaterial.Select(s => "  " + s));
            }
            if (noParam.Count > 0)
            {
                problems.Add($"Нет параметра {CodeParamName} или он недоступен для записи:");
                problems.AddRange(noParam.Select(s => "  " + s));
            }

            if (problems.Count == 0)
            {
                new InfoWindow280(head).ShowDialog();
            }
            else
            {
                foreach (string p in problems) Logger.Log(p, 4);
                var viewModel2 = new InfoWindowTextFieldViewModel();
                viewModel2.headtxt = head;
                viewModel2.ids = string.Join("\n", problems);
                viewModel2.lowtxt = "Эти элементы не заполнены.";
                new InfoWindowTextField(viewModel2).ShowDialog();
            }
            #endregion

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        /// <summary>Строка для фильтров по имени: семейство + тип.</summary>
        static string GetFilterName(Element e, Element type)
        {
            string family = (type as ElementType)?.FamilyName ?? "";
            string typeName = type?.Name ?? e.Name ?? "";
            return (family + " " + typeName).Trim();
        }

        static bool Contains(string s, string what) =>
            s != null && s.IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Материал: "Т Материал", затем "Материал несущих конструкций" — сначала у типа, затем у экземпляра.</summary>
        static Material GetMaterial(Document doc, Element e, Element type)
        {
            Element[] sources = type != null ? new[] { type, e } : new[] { e };
            foreach (Element src in sources)
            {
                Material m = MaterialFromParam(doc, src.LookupParameter(TMaterialParamName));
                if (m != null) return m;
            }
            foreach (Element src in sources)
            {
                Material m = MaterialFromParam(doc, src.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM));
                if (m != null) return m;
            }
            return null;
        }

        static Material MaterialFromParam(Document doc, Parameter p)
        {
            if (p == null || !p.HasValue || p.StorageType != StorageType.ElementId) return null;
            return doc.GetElement(p.AsElementId()) as Material;
        }

        /// <summary>
        /// Разбор имени вида "Бетон В25 W6 F150" → 1025006150.
        /// Класс В (лат./кир., дробный через точку или запятую), W и F обязательны.
        /// </summary>
        internal static bool TryParseCode(string materialName, out int code)
        {
            code = 0;
            if (string.IsNullOrWhiteSpace(materialName)) return false;

            double? b = null; int? w = null, f = null;
            foreach (string token in materialName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match m;
                if (b == null && (m = ClassRegex.Match(token)).Success)
                    b = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                else if (w == null && (m = WaterRegex.Match(token)).Success)
                    w = int.Parse(m.Groups[1].Value);
                else if (f == null && (m = FrostRegex.Match(token)).Success)
                    f = int.Parse(m.Groups[1].Value);
            }
            if (b == null || w == null || f == null) return false;

            long value = 1000000000L + (long)Math.Round(b.Value * 1000000) + w.Value * 1000L + f.Value;
            if (value > int.MaxValue) return false;
            code = (int)value;
            return true;
        }

        static bool TrySetCode(Element e, int code)
        {
            Parameter p = e.LookupParameter(CodeParamName);
            if (p == null || p.IsReadOnly) return false;
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.Integer: return p.Set(code);
                    case StorageType.Double: return p.Set((double)code);
                    case StorageType.String: return p.Set(code.ToString(CultureInfo.InvariantCulture));
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка записи {CodeParamName} у {e.Id}: {ex.Message}", 4);
                return false;
            }
        }
    }
}
