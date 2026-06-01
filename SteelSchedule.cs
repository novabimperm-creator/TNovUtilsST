using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using View = Autodesk.Revit.DB.View;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using TNovCommon;

namespace TNovUtilsST
{
    
    [Transaction(TransactionMode.Manual)]
    public class SteelSchedule : IExternalCommand
    {
        private void hidecolumn(in string value, out bool hide)
        {
            hide = false;
            if(value == "0") { try { hide = true; } catch (Exception) { hide = false; } }
            else if (value == "0.0") { try { hide = true; } catch (Exception) { hide = false; } }
            else if (value == "0,0") { try { hide = true; } catch (Exception) { hide = false; } }
            else if (value == "0.00") { try { hide = true; } catch (Exception) { hide = false; } }
            else if (value == "0,00") { try { hide = true; } catch (Exception) { hide = false; } }
        }

        private TNovProgressBar steelProgressBar;
        private void ThreadStartingPoint()
        {
            this.steelProgressBar = new TNovProgressBar();
            this.steelProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "ВРС подчистить";
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

            Logger.Log("Сбор элементов",1);

            List<ViewSchedule> schedules = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Schedules)   //фильтр по категории Спецификации
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<ViewSchedule>()                     //элементы категории Спецификации
                                                                         .ToList();                         //формируем список

            List<ViewSchedule> steelschedules = new List<ViewSchedule>();


            foreach (ViewSchedule schedule in schedules) //заполняем список ведомостей расхода стали
            {
                string name = schedule.Name;
                if (name.Contains("ВРС"))
                {
                    steelschedules.Add(schedule);
                }
                if (name.Contains("Ведомость расхода стали"))
                {
                    steelschedules.Add(schedule);
                }
            }
            #endregion

            #region Диалог

            Logger.Log("Диалоговое окно",1);
            //Диалог
            var viewModel = new SteelScheduleViewModel();
            // Десериализация
            bool forProject = false;
            json js = new json(in DBCommandName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<SteelScheduleViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new SteelScheduleWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { }
            else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            bool all = viewModel.all; bool active = viewModel.visible;
            #endregion

            Logger.Log("Проверяем, является ли открытый вид спецификацией",1);

            View v = doc.ActiveView;
            bool vrs = v.Name.Contains("ВРС") | v.Name.Contains("Ведомость расхода стали");
            bool isActiveView_vrs = v.Title.Contains("Спецификация")&& vrs;

            bool unhandledError = false;

            #region Основной код

            using (Transaction transaction = new Transaction(doc))
            {

                if (active == true) 
                {
                    if (isActiveView_vrs == false)
                    {
                        string info1txt = "Ошибка! Текущий открытый вид не является ведомостью расхода стали.\n" +
                            "Если все же является - щелкните мышью на любую из ячеек таблицы.\n" +
                            "В имени спецификации должно содержаться ВРС либо Ведомость расхода стали";
                        var info1 = new InfoWindow280(info1txt); info1.ShowDialog();
                        Logger.Log("Текущий вид не является ВРС. Завершение работы.",3);
                        return Result.Cancelled;
                    }
                    Logger.Log("Сценарий: активный вид",1);
                    ViewSchedule activeView = (ViewSchedule)uidoc.ActiveView;
                    Logger.Log("Ведомость: " + activeView.Name, 1);
                    try { 
                    transaction.Start("TNovUtilsST - ВРС подчистить");
                    Logger.Log("Открываем транзакцию", 1);

                    string[] names = new string[] { "", "Марка конструкции", "Напрягаемая арматура класса", "Изделия арматурные", "Изделия закладные" }; //заголовки
                    int cc = activeView.Definition.GetFieldCount();

                    for (int i = 0; i < cc; i++)
                    {
                        activeView.Definition.GetField(i).IsHidden = false; //включаем все поля
                    }
                    TableSectionData tb = activeView.GetTableData().GetSectionData(SectionType.Body);
                    TableSectionData th = activeView.GetTableData().GetSectionData(SectionType.Header);

                    int nc = tb.NumberOfColumns;
                    int nr = tb.NumberOfRows;
                    for (int c = 0; c < nc; c++) //столбец
                    {
                        ScheduleField sf = activeView.Definition.GetField(c);
                        string heading = tb.GetCellText(0, c); //группа заголовков столбца (самая верхняя ячейка столбца)
                        bool hide = true;
                        for (int r = 0; r < nr; r++) //строка
                        {
                            if (tb.GetCellType(r, c) == CellType.ParameterText)
                            {
                                string tbt = tb.GetCellText(r, c); //значение в ячейке
                                hidecolumn(in tbt, out hide); //нужно ли скрывать ячейку
                                if (hide == false) { break; } //на первой же ячейке с ненулевым значением решаем не скрывать столбец
                            }
                        }
                        if (hide == true) { sf.IsHidden = true; } //скрываем столбцы, где все значения нулевые

                        bool headcontainsfield = false;
                        foreach (string name in names) //проходим по списку заголовков names
                        {
                            if (name == heading) { headcontainsfield = true; }
                        }
                        if (headcontainsfield == false) { sf.IsHidden = true; } //скрываем столбцы, не входящие в заголовки names
                    }
                    
                    transaction.Commit();
                    Logger.Log("Закрываем транзакцию", 1);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Ошибка: " + ex.Message, 4);
                        new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                        unhandledError = true;
                    }
                }
                else
                {
                    Logger.Log("Сценарий: все ВРС", 1);

                    Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();
                    Thread.Sleep(100);

                    int PBCount = 0; int allcount= steelschedules.Count;
                    this.steelProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.steelProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                    this.steelProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.steelProgressBar.value.Text = PBCount.ToString()));
                    this.steelProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.steelProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                    this.steelProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.steelProgressBar.maxvalue.Text = allcount.ToString()));

                    try { 
                    transaction.Start("TNovUtilsST - ВРС подчистить");
                    Logger.Log("Открываем транзакцию", 1);

                    foreach (ViewSchedule schedule in steelschedules) //проходим по каждой ВРС
                    {
                        
                        Logger.Log("Ведомость: "+ schedule.Name, 1);
                        string[] names = new string[] { "", "Марка конструкции", "Напрягаемая арматура класса", "Изделия арматурные", "Изделия закладные" }; //заголовки
                        int cc = schedule.Definition.GetFieldCount();

                        for (int i = 0; i < cc; i++)
                        {
                            schedule.Definition.GetField(i).IsHidden = false; //включаем все поля
                        }
                        TableSectionData tb = schedule.GetTableData().GetSectionData(SectionType.Body);
                        TableSectionData th = schedule.GetTableData().GetSectionData(SectionType.Header);

                        int nc = tb.NumberOfColumns;
                        int nr = tb.NumberOfRows;
                        for (int c = 0; c < nc; c++) //столбец
                        {
                            ScheduleField sf = schedule.Definition.GetField(c);
                            string heading = tb.GetCellText(0, c); //группа заголовков столбца (самая верхняя ячейка столбца)
                            bool hide = true;
                            for (int r = 0; r < nr; r++) //строка
                            {
                                if (tb.GetCellType(r, c) == CellType.ParameterText)
                                {
                                    string tbt = tb.GetCellText(r, c); //значение в ячейке
                                    hidecolumn(in tbt, out hide); //нужно ли скрывать ячейку
                                    if (hide == false) { break; } //на первой же ячейке с ненулевым значением решаем не скрывать столбец
                                }
                            }
                            if (hide == true) { sf.IsHidden = true; } //скрываем столбцы, где все значения нулевые

                            bool headcontainsfield = false;
                            foreach (string name in names) //проходим по списку заголовков names
                            {
                                if (name == heading) { headcontainsfield = true; }
                            }
                            if (headcontainsfield == false) { sf.IsHidden = true; } //скрываем столбцы, не входящие в заголовки names
                        }
                        PBCount++;
                        this.steelProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.steelProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.steelProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.steelProgressBar.value.Text = PBCount.ToString()));

                    }

                    //var info1 = new InfoWindow280("Успешно!\nВсе ВРС в проекте подчищены"); info1.ShowDialog();
                    transaction.Commit();
                    
                    Logger.Log("Закрываем транзакцию",1);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Ошибка: " + ex.Message, 4);
                        new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                        unhandledError = true;
                    }
                    finally
                    {
                        CloseProgressBarSafely();
                    }
                }
            
                
            }
            #endregion

            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
        private void CloseProgressBarSafely()
        {
            if (steelProgressBar != null &&
                steelProgressBar.Dispatcher != null &&
                !steelProgressBar.Dispatcher.HasShutdownStarted)
            {
                steelProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (steelProgressBar.IsLoaded)
                        steelProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
