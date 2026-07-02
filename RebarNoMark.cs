using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovUtilsST
{
    [Transaction(TransactionMode.Manual)]
    public class RebarNoMark : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Арматура без марки";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " ");
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            docName = docName.Replace(",", "");
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);

            Guid adskCMarkParamGuid = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"); //A_Марка конструкции - заменено на гуид
            View activeView = doc.ActiveView;

            // ПРОВЕРКА: Активный вид должен быть 3D
            if (!(activeView is View3D))
            {
                new InfoWindow280("Пожалуйста, откройте 3D-вид перед запуском команды.").ShowDialog();
                return Result.Failed;
            }

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

            // Собираем ВСЮ арматуру в документе
            FilteredElementCollector allRebarsCollector = new FilteredElementCollector(doc);
            List<Element> allRebars = allRebarsCollector
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .ToList();

            // Находим стержни с пустым параметром
            List<ElementId> rebarsToIsolate = new List<ElementId>();

            string paramName = "";
            foreach (Element rebar in allRebars)
            {
                string paramValue = Param.GetStringParamValue(doc,adskCMarkParamGuid,rebar);

                if (paramValue==null||paramValue=="")
                {
                    rebarsToIsolate.Add(rebar.Id);
                }

                if (paramName == "") //добавлено: заполняем имя параметра (исходя из гуид) для выводимого сообщения
                {
                    if (Param.ParamExistByGuid(adskCMarkParamGuid, rebar))
                    {
                        paramName = rebar.get_Parameter(adskCMarkParamGuid).Definition.Name;
                    }
                }
            }

            if (rebarsToIsolate.Count == 0)
            {
                new InfoWindow280($"Арматура с пустым параметром '{paramName}' не найдена.");
                Logger.Log($"Арматура с пустым параметром '{paramName}' не найдена. Завершение работы.", 5);
                return Result.Succeeded;
            }
            #endregion

            Logger.Log($"Найдено {rebarsToIsolate.Count.ToString()} элементов с пустым параметром '{paramName}'", 1);

            #region Основной код
            // Применяем изоляцию на 3D-виде (исправленная версия)
            using (Transaction trans = new Transaction(doc, "Изолировать арматуру на 3D-виде"))
            {
                trans.Start();

                // Способ 1: Используем IsolateElementsTemporary (рекомендуется)
                try
                {
                    activeView.IsolateElementsTemporary(rebarsToIsolate);
                    Logger.Log("Сработал основной метод изоляции");
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Если IsolateElementsTemporary не сработал, используем альтернативный метод
                    AlternativeIsolation(activeView, rebarsToIsolate, doc);
                    Logger.Log("Задействован альтернативный метод изоляции");
                }

                trans.Commit();
            }
            #endregion

            new InfoWindow280($"На 3D-виде изолировано элементов: {rebarsToIsolate.Count}").ShowDialog();

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        // Альтернативный метод изоляции (ручное скрытие с фильтрацией)
        private void AlternativeIsolation(View activeView, List<ElementId> elementsToKeep, Document doc)
        {
            // Получаем все элементы на виде
            FilteredElementCollector allElementsCollector = new FilteredElementCollector(doc, activeView.Id);
            ICollection<ElementId> allElementIds = allElementsCollector.ToElementIds();

            List<ElementId> elementsToHide = new List<ElementId>();

            // Фильтруем элементы, которые МОЖНО скрыть
            foreach (ElementId id in allElementIds)
            {
                Element element = doc.GetElement(id);

                // Проверяем, можно ли скрыть этот элемент
                if (element != null && CanBeHidden(element))
                {
                    // Если элемент НЕ в списке для изоляции - скрываем
                    if (!elementsToKeep.Contains(id))
                    {
                        elementsToHide.Add(id);
                    }
                }
            }

            // Скрываем только те элементы, которые можно скрыть
            if (elementsToHide.Count > 0)
            {
                activeView.HideElements(elementsToHide);
            }

            // Убеждаемся, что нужные элементы видны
            activeView.UnhideElements(elementsToKeep);
        }

        // Проверяем, можно ли скрыть элемент
        private bool CanBeHidden(Element element)
        {

            // Категории, которые НЕЛЬЗЯ скрыть (исправленные названия)
            BuiltInCategory[] nonHideableCategories = new BuiltInCategory[]
            {
                    BuiltInCategory.OST_Levels,           // Уровни
                    BuiltInCategory.OST_Grids,             // Сетки
                    BuiltInCategory.OST_ReferenceLines,    // Референсные линии
                    BuiltInCategory.OST_Viewers,           // Виды-зависимости
                    BuiltInCategory.OST_Sections,          // Разрезы
                    BuiltInCategory.OST_Viewers,           // Фасады (в API нет отдельной категории для фасадов)
                    BuiltInCategory.OST_Callouts,          // Выноски
                    BuiltInCategory.OST_AreaSchemes,       // Схемы зон (исправлено: OST_AreaSchecs -> OST_AreaSchemes)
                    BuiltInCategory.OST_RoomTags,          // Марки помещений
                    BuiltInCategory.OST_AreaTags,          // Марки зон
                    BuiltInCategory.OST_SpotElevations,    // Отметки высот
                    BuiltInCategory.OST_SpotCoordinates,   // Координаты
                    BuiltInCategory.OST_Viewports          // Видовые экраны на листах
            };

            // Проверяем категорию элемента
            Category category = element.Category;
            if (category != null)
            {
                BuiltInCategory bic = (BuiltInCategory)RevitApiCompat.ElementIdIntValue(category.Id);
                if (nonHideableCategories.Contains(bic))
                {
                    return false;
                }
            }

            // Дополнительные проверки
            // Нельзя скрыть View (сам вид) и его зависимости
            if (element is View)
            {
                return false;
            }

            // Элементы без категории (Internal)
            if (element.Category == null)
            {
                return false;
            }

            return true;
        }

    }
}
