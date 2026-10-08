using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TNovCommon;

namespace TNovUtilsST
{
    /// <summary>
    /// Подчистка открытой ведомости расхода материалов: скрывает служебные столбцы
    /// и столбцы, где все значения пустые или нулевые (замена Dynamo-скрипта "ВРМ подчистить.dyn").
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MaterialSchedule : IExternalCommand
    {
        //служебные столбцы, скрываются всегда
        static readonly string[] HiddenHeaders = { "N_Код материала", "N_Эт.Номер", "Материал: Объем", "Категория" };
        //первые столбцы (наименование и т.п.) не анализируются
        const int FirstAnalyzedColumn = 2;

        static readonly Regex LeadingNumber = new Regex(@"^-?\d+(?:[.,]\d+)?");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "ВРМ подчистить";
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

            Logger.Log("Проверяем, является ли открытый вид спецификацией", 1);

            ViewSchedule schedule = uidoc.ActiveView as ViewSchedule;
            if (schedule == null || schedule.IsTitleblockRevisionSchedule || schedule.Definition == null)
            {
                new InfoWindow280("Откройте ведомость расхода материалов (спецификацию) и запустите команду еще раз.\n" +
                    "Если она уже открыта — щелкните мышью на любую из ячеек таблицы.").ShowDialog();
                Logger.Log("Текущий вид не является спецификацией. Завершение работы.", 3);
                return Result.Cancelled;
            }
            Logger.Log("Ведомость: " + schedule.Name, 1);

            #region Основной код
            int hiddenCount = 0;
            using (Transaction transaction = new Transaction(doc, "TNovUtilsST - ВРМ подчистить"))
            {
                try
                {
                    transaction.Start();

                    ScheduleDefinition def = schedule.Definition;
                    int fieldCount = def.GetFieldCount();
                    for (int i = 0; i < fieldCount; i++) def.GetField(i).IsHidden = false; //включаем все поля
                    doc.Regenerate();

                    TableSectionData tb = schedule.GetTableData().GetSectionData(SectionType.Body);
                    int nc = Math.Min(tb.NumberOfColumns, fieldCount);
                    int nr = tb.NumberOfRows;

                    for (int c = FirstAnalyzedColumn; c < nc; c++)
                    {
                        ScheduleField sf = def.GetField(c);
                        string heading = tb.GetCellText(0, c);

                        bool hide = IsHiddenHeader(heading) || IsHiddenHeader(sf.ColumnHeading) || IsHiddenHeader(sf.GetName());
                        if (!hide)
                        {
                            hide = true;
                            for (int r = 0; r < nr; r++)
                            {
                                if (tb.GetCellType(r, c) == CellType.ParameterText && !IsEmptyOrZero(tb.GetCellText(r, c)))
                                {
                                    hide = false; //на первой же непустой ячейке оставляем столбец
                                    break;
                                }
                            }
                        }

                        if (hide) { sf.IsHidden = true; hiddenCount++; }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    if (transaction.HasStarted()) transaction.RollBack();
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    Logger.Log("Завершение работы с ошибками.", 4);
                    return Result.Succeeded;
                }
            }
            #endregion

            Logger.Log($"Скрыто столбцов: {hiddenCount}", 1);
            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        static bool IsHiddenHeader(string header) =>
            !string.IsNullOrEmpty(header) && HiddenHeaders.Contains(header.Trim());

        /// <summary>Пустое значение или ноль (в том числе с единицами измерения: "0,00 м³").</summary>
        static bool IsEmptyOrZero(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            Match m = LeadingNumber.Match(value.Trim());
            if (!m.Success) return false;
            return double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d == 0;
        }
    }
}
