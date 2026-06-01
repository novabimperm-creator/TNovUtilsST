using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;

using System.Linq;
using System;
using Rebar = Autodesk.Revit.DB.Structure.Rebar;
using Parameter = Autodesk.Revit.DB.Parameter;
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
    public class Schemespec : IExternalCommand
    {
        string output1; // --версия 1.1.4--
        string output2;
        string output3;
        string output4;
        string output5;
        string output6;
        string output7;
        string output8;
        string output9;
        string output10;
        string output11;
        string output12;
        string output13;
        string output14;
        string output15;
        string output16;
        string output17;
        string output18;
        string output19;

        
        
        String MarkGroup(in string mark, out string group)
        {
            group = "не назначено";
            if (mark.StartsWith("Рм")) { group = output1; }
            if (mark.StartsWith("Рл")) { group = output2; }
            if (mark.StartsWith("Пр")) { group = output3; }
            if (mark.StartsWith("Пл")) { group = output4; }
            if (mark.StartsWith("Фп")) { group = output5; }
            if (mark.StartsWith("Пол")) { group = output6; }
            if (mark.StartsWith("Пб")) { group = output6; }
            if (mark.StartsWith("Мс")) { group = output7; }
            if (mark.StartsWith("ЛК")) { group = output8; }
            if (mark.StartsWith("Лк")) { group = output8; }
            if (mark.StartsWith("Лм")) { group = output9; }
            if (mark.StartsWith("Лп")) { group = output10; }
            if (mark.StartsWith("Дж")) { group = output11; }
            if (mark.StartsWith("К")) { group = output12; }
            if (mark.StartsWith("Кл")) { group = output12; }
            if (mark.StartsWith("Пм")) { group = output13; }
            if (mark.StartsWith("Пп")) { group = output14; }
            if (mark.StartsWith("Бм")) { group = output15; }
            if (mark.StartsWith("Пт")) { group = output16; }
            if (mark.StartsWith("Дс")) { group = output17; }
            if (mark.StartsWith("Км")) { group = output18; }
            if (mark.StartsWith("Вып")) { group = output19; }
            return group;
        }
        private TNovProgressBar schemespecProgressBar;
        private void ThreadStartingPoint()
        {
            this.schemespecProgressBar = new TNovProgressBar();
            this.schemespecProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Группировка";
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


            //параметры
            Guid adskCMarkParamGuid = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"); //A_Марка конструкции
            Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование

            #region Сбор элементов
            Logger.Log("Сбор элементов",1);
            
            List<FamilyInstance> beams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)   //Каркас несущий
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<FamilyInstance> columns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)   //Несущие колонны
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<Wall> walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)   //Стены
                                                                         .WhereElementIsNotElementType()    
                                                                         .OfClass(typeof(Wall))         //отсеиваем модели в контексте
                                                                         .Cast<Wall>()                     
                                                                         .ToList();
            
            List<Autodesk.Revit.DB.Floor> floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors)   //Перекрытия
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            
            List<Autodesk.Revit.DB.Architecture.Stairs> stairs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)   //Лестницы
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Architecture.Stairs))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Architecture.Stairs>()
                                                                         .ToList();
            
            List<FamilyInstance> stairs2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)   //Лестницы семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<Autodesk.Revit.DB.Architecture.Railing> railings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)   //Ограждения
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Architecture.Railing)) //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Architecture.Railing>()
                                                                         .ToList();
            
            List<FamilyInstance> railings2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)   //Ограждения семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<Autodesk.Revit.DB.Floor> foundations = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            
            List<FamilyInstance> foundations2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<Autodesk.Revit.DB.WallFoundation> foundations3 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты ленточные
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.WallFoundation))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.WallFoundation>()
                                                                         .ToList();
            
            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //Об мод
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<FamilyInstance> structconnections = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructConnections)   //Болты фунд
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            
            List<Rebar> rebar = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rebar)   //Несущая арматура
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Rebar))  //отсеиваем модели в контексте
                                                                         .Cast<Rebar>()
                                                                         .ToList();
            
            List<FamilyInstance> rebar2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rebar)   //Несущая арматура семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))  
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilyInstance> FIs = new List<FamilyInstance>(beams.Count + columns.Count + stairs2.Count+
                railings2.Count+ foundations2.Count+ GMs.Count+ structconnections.Count); //общий список загруж семейств кроме арматуры
            FIs.AddRange(beams); FIs.AddRange(columns); FIs.AddRange(stairs2); FIs.AddRange(railings2);
            FIs.AddRange(foundations2); FIs.AddRange(GMs); FIs.AddRange(structconnections);
            #endregion

            #region Диалог
            Logger.Log("Элементы собраны. Диалог",1);

            //Диалог
            var viewModel = new SchemespecViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in DBCommandName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<SchemespecViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new SchemespecWPF(viewModel);
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

            output1 = viewModel.output1; output2 = viewModel.output2; output3 = viewModel.output3;
            output4 = viewModel.output4; output5 = viewModel.output5; output6 = viewModel.output6;
            output7 = viewModel.output7; output8 = viewModel.output8; output9 = viewModel.output9;
            output10 = viewModel.output10; output11 = viewModel.output11; output12 = viewModel.output12;
            output13 = viewModel.output13; output14 = viewModel.output14; output15 = viewModel.output15;
            output16 = viewModel.output16; output17 = viewModel.output17; output18 = viewModel.output18;
            output19 = viewModel.output19;
            #endregion

            int failscount = 0;
            List<string> failed = new List<string>(); //пустой список id элементов недоступных для редактирования

            int allcount = walls.Count + floors.Count + stairs.Count + railings.Count + foundations.Count + foundations3.Count + rebar.Count + FIs.Count+ rebar2.Count;

            //проверка наличия элементов
            if (allcount == 0)
            {
                new InfoWindow280("В данной модели отсутствуют элементы для обработки.").ShowDialog();
                Logger.Log("Элементы в модели отсутствуют. Завершение работы.", 3); return Result.Cancelled;
            }

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
            this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.maxvalue.Text = allcount.ToString()));

            bool unhandledError = false;

            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try { 
                transaction.Start("Группировка и параметры");
                Logger.Log("Открываем транзакцию",1);

                Logger.Log("Стены:",1);
                foreach (Wall elem in walls) 
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);
                    
                    //группируем по маркам
                    string Parvalue_group = "-";
                    if( Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент "+ eid+": марка "+ Parvalue_mrk+", назначена группа "+ Parvalue_group,2);
                    }
                    catch (Exception ex) 
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: "+ex.Message,4); 
                        failed.Add(eid); failscount++; continue; 
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Плиты:", 1);
                foreach (Autodesk.Revit.DB.Floor elem in floors)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group, 2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message, 4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Лестницы:", 1);
                foreach (Autodesk.Revit.DB.Architecture.Stairs elem in stairs)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group,2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message, 4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Ограждения:", 1);
                foreach (Autodesk.Revit.DB.Architecture.Railing elem in railings)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group, 2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message,4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Фундаменты:", 1);
                foreach (Autodesk.Revit.DB.Floor elem in foundations)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group,2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message, 4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Фундаменты ленточные:", 1);
                foreach (Autodesk.Revit.DB.WallFoundation elem in foundations3)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group,2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message, 4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Загружаемые семейства:", 1);
                foreach (FamilyInstance elem in FIs)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group,2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message,4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                //Арматура: только группирование
                Logger.Log("Арматура системная:", 1);
                foreach (Rebar elem in rebar)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group,2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message,4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }

                Logger.Log("Арматура семействами:", 1);
                foreach (FamilyInstance elem in rebar2)
                {
                    //получаем параметры
                    string eid = elem.Id.ToString();
                    string Parvalue_mrk = elem.get_Parameter(adskCMarkParamGuid).AsValueString();
                    Parameter Par_group = elem.get_Parameter(adskGparamGuid);

                    //группируем по маркам
                    string Parvalue_group = "-";
                    if (Parvalue_mrk != null)
                    {
                        MarkGroup(Parvalue_mrk, out Parvalue_group);
                    }
                    else { Parvalue_mrk = "-"; }

                    //назначаем Группу
                    try
                    {
                        Par_group.Set(Parvalue_group);
                        Logger.Log("   Элемент " + eid + ": марка " + Parvalue_mrk + ", назначена группа " + Parvalue_group,2);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("   Элемент " + eid + " Ошибка: " + ex.Message, 4);
                        failed.Add(eid); failscount++; continue;
                    }
                    PBCount++;
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.schemespecProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.schemespecProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.schemespecProgressBar.value.Text = PBCount.ToString()));
                }
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

                if (failscount > 0) 
                {
                    Logger.Log("Открываем окно с ID проблемных элементов: " + String.Join(",", failed), 1);
                    // Диалоговое окно
                    ElementsTreeWindow window = new ElementsTreeWindow(uiApp, String.Join(",", failed), DBCommandName, dateTime,TNovVersion);
                    window.Show();
                    
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
            if (schemespecProgressBar != null &&
                schemespecProgressBar.Dispatcher != null &&
                !schemespecProgressBar.Dispatcher.HasShutdownStarted)
            {
                schemespecProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (schemespecProgressBar.IsLoaded)
                        schemespecProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
