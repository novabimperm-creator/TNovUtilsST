using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Windows.Threading;
using System.Threading;
using Newtonsoft.Json;
using System.IO;
using TNovCommon;

namespace TNovUtilsST
{
    [Transaction(TransactionMode.Manual)]
    public class Fixstructurefile : IExternalCommand
    {
        private TNovProgressBar fixProgressBar;
        private void ThreadStartingPoint()
        {
            this.fixProgressBar = new TNovProgressBar();
            this.fixProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Ускорить файл КЖ";
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
            //получаем все типы арматуры
            List<RebarBarType> rebarTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();
            Logger.Log("Найдены типы арматуры в кол-ве "+ rebarTypes.Count.ToString()+" шт",1);
            
            if (rebarTypes.Count == 0)
            {
                new InfoWindow280("В данной модели отсутствуют типы арматурных стержней!").ShowDialog();
                Logger.Log("Отсутствуют типы арматурных стержней. Завершение работы.", 3);
                return Result.Failed;
            }

            Logger.Log("Собираем общие параметры проекта, добавленные для арматуры по типу",1);
            Dictionary<string, TNovParameter> projectParamsStorage = new Dictionary<string, TNovParameter>();
            RebarBarType firstBarType = rebarTypes.First();
            foreach (Parameter param in firstBarType.ParametersMap)
            {
                string paramName = param.Definition.Name;
                if (!param.IsShared) continue;
                TNovParameter mpsp = new TNovParameter(param, doc);
                projectParamsStorage.Add(paramName, mpsp);
                Logger.Log("Общий параметр найден: "+paramName,2);
            }

            Logger.Log("Запоминаем типы арматуры",1);
            //запоминаем все типы арматуры со значениями параметров
            List<TNovRebarType> myrebarTypes = new List<TNovRebarType>();

            foreach (RebarBarType rbt in rebarTypes)
            {
                Logger.Log("Тип " + rbt.Name,2);
                try
                {
                    ParameterMap parameterMap = rbt.ParametersMap; //обход ошибки в API
                }
                catch (Exception ex) 
                {
                    Logger.Log("   ошибка: "+ex.Message,4); continue;
                }
                TNovRebarType mrt = new TNovRebarType(rbt);
                myrebarTypes.Add(mrt);
                Logger.Log("   _MyRebarType сохранен",2);
            }

            Logger.Log("Открываем ФОП",1);
            DefinitionFile deffile = null;
            try
            {
                deffile = commandData.Application.Application.OpenSharedParameterFile();
            }
            catch
            {
                var info1 = new InfoWindow280("Не найден файл общих параметров!"); info1.ShowDialog();
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/startraboty/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                Logger.Log("Не найден файл общих параметров. Завершение работы.",3);
                return Result.Cancelled;
            }

            if (deffile == null)
            {
                var info1 = new InfoWindow280("Некорректный файл общих параметров!"); info1.ShowDialog();
                Logger.Log("Некорректный файл общих параметров. Завершение работы.",3);
                return Result.Cancelled;
            }
            #endregion

            int allcount =projectParamsStorage.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = PBCount.ToString()));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.maxvalue.Text = allcount.ToString()));

            bool unhandledError = false;
            #region Основной код
            try
            {
                //удаляем параметр проекта (если только 1 категория) или снимаем флажок с категории несущей арматуры (если категорий несколько)
                Logger.Log("Чистим параметры арматуры в проекте", 1);
                using (Transaction t = new Transaction(doc))
                {
                    Logger.Log("Открываем транзакцию 1 (удалить параметры)", 1);
                    t.Start("TNovUtilsST - ускорить файл КЖ (1 этап)");
                    {
                        foreach (var kvp in projectParamsStorage)
                        {
                            PBCount++;
                            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = "Удаление параметров " + PBCount.ToString()));


                            TNovParameter param = kvp.Value;
                            if (param.TNovParamCategories.Count == 1)
                            {
                                //параметр только для несущей арматуры, значит надо удалить целиком
                                //перед этим проверяем, есть ли параметр в ФОП

                                bool checkParamExistsInDefFile = CheckParameterExistsInFile(deffile, param.TNovParamGuid);
                                if (!checkParamExistsInDefFile)
                                {
                                    AddParameterToDefFile(deffile, "NonTemplate parameters", param);
                                }


                                doc.ParameterBindings.Remove(param.TNovParamDefinition);
                                Logger.Log("   Удален: " + param.TNovParamName, 2);
                            }
                            else
                            {
                                //категорий несколько, надо убрать флажок с категории несущей арматуры
                                param.RemoveOrAddFromRebarCategory(doc, firstBarType, false);
                                Logger.Log("   Снят флажок с несущей арматуры: " + param.TNovParamName, 2);
                            }
                        }
                    }
                    t.Commit();
                    Logger.Log("Закрываем транзакцию 1", 1);
                }

                Logger.Log("Параметры удалены, возвращаем обратно", 1);
                PBCount = 0;
                this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));


                //возвращаем параметры обратно
                using (Transaction t2 = new Transaction(doc))
                {
                    t2.Start("TNovUtilsST - ускорить файл КЖ (2 этап)");
                    Logger.Log("Открываем транзакцию 2 (возвращение параметров)", 1);

                    foreach (var kvp in projectParamsStorage)
                    {
                        PBCount++;
                        this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = "Возвращение параметров " + PBCount.ToString()));

                        TNovParameter param = kvp.Value;
                        if (param.TNovParamCategories.Count == 1)
                        {
                            //параметр был назначен только несущей арматуре, был удален совсем, значит создаем параметр
                            param.AddToProjectParameters(doc, firstBarType);
                            Logger.Log("   Добавлен: " + param.TNovParamName, 2);
                        }
                        else
                        {
                            //категорий было несколько, возвращаем флажок к категории несущей арматуры
                            param.RemoveOrAddFromRebarCategory(doc, firstBarType, true);
                            Logger.Log("   Добавлен флажок для несущей арматуры: " + param.TNovParamName, 2);
                        }
                    }

                    t2.Commit();
                    Logger.Log("Закрываем транзакцию 2", 1);
                }

                Logger.Log("Восстанавливаем значения параметров", 1);
                PBCount = 0; allcount = myrebarTypes.Count;
                this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.maxvalue.Text = allcount.ToString()));

                //восстанавливаем значения у типов арматуры
                using (Transaction t3 = new Transaction(doc))
                {
                    t3.Start("TNovUtilsST - ускорить файл КЖ (3 этап)");
                    Logger.Log("Открываем транзакцию 3 (возвращение значений)", 1);

                    foreach (TNovRebarType mrt in myrebarTypes)
                    {
                        PBCount++;
                        this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = "Возвращение значений " + PBCount.ToString()));

                        RebarBarType rbt = mrt.bartype;
                        Logger.Log("Тип: " + mrt.Name, 2);

                        foreach (Parameter param in rbt.ParametersMap)
                        {
                            string paramName = param.Definition.Name;
                            TNovParameterValue mpv = mrt.ValuesStorage[paramName];
                            if (mpv.IsNull) continue;
                            mpv.SetValue(param);
                            Logger.Log("   Параметр: " + paramName + ", значение " + mpv.ToString(), 2);
                        }
                    }

                    t3.Commit();
                    Logger.Log("Закрываем транзакцию 3", 1);
                }
                //string endTime = DateTime.Now.ToLongTimeString();
                //string msg = "Выполнено! Время старта: " + startTime + ", окончания: " + endTime;

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
            #endregion

            //var info2 = new InfoWindow280("Успешно! Файл станет быстрее."); info2.ShowDialog();
            //Debug.WriteLine(msg);
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
            if (fixProgressBar != null &&
                fixProgressBar.Dispatcher != null &&
                !fixProgressBar.Dispatcher.HasShutdownStarted)
            {
                fixProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (fixProgressBar.IsLoaded)
                        fixProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }

        static bool CheckParameterExistsInFile(DefinitionFile deffile, Guid paramGuid)
        {
            if (deffile == null)
            {
                throw new Exception("Не подключен файл общих параметров");
            }
            foreach (DefinitionGroup defgr in deffile.Groups)
            {
                foreach (ExternalDefinition exdf in defgr.Definitions)
                {
                    if (paramGuid.Equals(exdf.GUID))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static ExternalDefinition AddParameterToDefFile(DefinitionFile defFile, string groupName, TNovParameter myparam)
        {
            DefinitionGroup tempGroup = null;
            List<DefinitionGroup> groups = defFile.Groups.Where(i => i.Name == groupName).ToList();
            if (groups.Count == 0)
            {
                try
                {
                    tempGroup = defFile.Groups.Create(groupName);
                }
                catch (Exception)
                {
                    throw new Exception("Не удалось создать группу " + groupName + " в файле общих параметров " + defFile.Filename);
                }
            }
            else
            {
                tempGroup = groups.First();
            }


            Definitions defs = tempGroup.Definitions;
            ExternalDefinitionCreationOptions defOptions =
                  new ExternalDefinitionCreationOptions(myparam.TNovParamName, myparam.TNovParamDefinition.ParameterType);
            defOptions.GUID = myparam.TNovParamGuid;

            ExternalDefinition exDef = defs.Create(defOptions) as ExternalDefinition;
            if (exDef == null)
            {
                throw new Exception("Не удалось создать общий параметр " + myparam.TNovParamName);
            }
            return exDef;
        }

    }
}
