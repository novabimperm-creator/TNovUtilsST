using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TNovUtilsST
{
    public class TNovParameter
    {
        public string TNovParamName { get; set; }
        public Definition TNovParamDefinition;
        public List<Category> TNovParamCategories = new List<Category>();
        public BuiltInParameterGroup TNovParamGroup;
        public Guid TNovParamGuid;

        public TNovParameter(Parameter param, Document doc)
        {

            TNovParamDefinition = param.Definition;
            TNovParamName = TNovParamDefinition.Name;

            InternalDefinition intDef = TNovParamDefinition as InternalDefinition;
            if (intDef != null) TNovParamGroup = intDef.ParameterGroup;

            TNovParamGuid = param.GUID;


            ElementBinding elemBind = this.GetBindingByParamName(TNovParamName, doc);

            foreach (Category cat in elemBind.Categories)
            {
                TNovParamCategories.Add(cat);
            }
        }

        public bool RemoveOrAddFromRebarCategory(Document doc, Element elem, bool addOrDeleteCat)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

            ElementBinding elemBind = this.GetBindingByParamName(TNovParamName, doc);

            CategorySet newCatSet = app.Create.NewCategorySet();
            int rebarcatid = new ElementId(BuiltInCategory.OST_Rebar).IntegerValue;
            foreach (Category cat in elemBind.Categories)
            {
                int catId = cat.Id.IntegerValue;
                if (catId != rebarcatid)
                {
                    newCatSet.Insert(cat);
                }
            }

            if (addOrDeleteCat)
            {
                Category cat = elem.Category;
                newCatSet.Insert(cat);
            }

            TypeBinding newBind = app.Create.NewTypeBinding(newCatSet);
            if (doc.ParameterBindings.Insert(TNovParamDefinition, newBind, TNovParamGroup))
            {
                return true;
            }
            else
            {
                if (doc.ParameterBindings.ReInsert(TNovParamDefinition, newBind, TNovParamGroup))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void AddToProjectParameters(Document doc, Element elem)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            //string oldSharedParamsFile = app.SharedParametersFilename;

            //Проверка актуальности шаблона

            ProjectInfo projectInfo = doc.ProjectInformation;
            //Guid guid = new Guid ("ae46eb7a - 03bf - 497e-ac96 - 1615c672324b");
            Autodesk.Revit.DB.Parameter template = projectInfo.LookupParameter("N_Орг.ВерсияШаблона");
            bool oldProject = false;
            string templateversion = "v";
            if (template == null) { oldProject = true; }
            else { templateversion = template.AsValueString(); }
            templateversion = templateversion.Replace(" (Talan)", "");
            templateversion = templateversion.Replace("(Talan)", "");
            templateversion = templateversion.Replace(" (UDS)", "");
            templateversion = templateversion.Replace("(UDS)", "");
            if (templateversion.Contains("v"))
            {
                oldProject = true;
            }
            else
            {
                string[] versionparts = templateversion.Split('.');
                double versionMath = Convert.ToDouble(versionparts[0]) * 10 + Convert.ToDouble(versionparts[1]);
                if (versionMath < 20223) { oldProject = true; }
            }

            string Name0 = TNovParamName; //запоминаем имя параметра как в проекте
            
            if (oldProject == true) //меняем имя параметра, чтобы забрать его "обратно" из ФОП
            {
                if (TNovParamName == "Арм.ВыполненаСемейством") { TNovParamName = "A_Арм семейством"; }
                if (TNovParamName == "Рзм.Диаметр") { TNovParamName = "A_Размер_Диаметр"; }
                if (TNovParamName == "Арм.КлассЧисло") { TNovParamName = "A_Код металлопроката"; }
                if (TNovParamName == "Мрк.НаименованиеИзделия") { TNovParamName = "W_Мрк.НаименованиеИзделия"; }
                if (TNovParamName == "Арм.Обозначение") { TNovParamName = "N_Арм.Обозначение"; }
                if (TNovParamName == "Мрк.ПозАрматурыПМ") { TNovParamName = "W_Мрк.ПозАрматурыПМ"; }
                if (TNovParamName == "Наименование") { TNovParamName = "N_Наименование"; }
                if (TNovParamName == "Обозначение") { TNovParamName = "N_Обозначение"; }
                if (TNovParamName == "Рзм.ПогМетрыВкл") { TNovParamName = "A_ПогМетрыВкл"; }
                if (TNovParamName == "Орг.СпособПодсчетаМассы") { TNovParamName = "A_Способ подсчета массы"; }
                if (TNovParamName == "Орг.ИзделиеТипПодсчета") { TNovParamName = "A_Тип элемента КЖ"; }
                if (TNovParamName == "Арм.ТипИзделия") { TNovParamName = "W_Арм.ТипИзделия"; }
            }


            ExternalDefinition exDef = null;
            string sharedFile = app.SharedParametersFilename;
            DefinitionFile sharedParamFile = app.OpenSharedParameterFile();
            foreach (DefinitionGroup defgroup in sharedParamFile.Groups)
            {
                foreach (Definition def in defgroup.Definitions)
                {
                    if (def.Name == TNovParamName)
                    {
                        exDef = def as ExternalDefinition;
                    }
                }
            }
            if (exDef == null) throw new Exception("В файле общих параметров не найден общий параметр " + TNovParamName);

            TNovParamName = Name0; //возвращаем имя параметра как в проекте для дальнейших действий

            CategorySet catSet = app.Create.NewCategorySet();
            catSet.Insert(elem.Category);
            TypeBinding newBind = app.Create.NewTypeBinding(catSet);

            doc.ParameterBindings.Insert(exDef, newBind, TNovParamGroup);

            //app.SharedParametersFilename = oldSharedParamsFile;

            Parameter testParam = elem.LookupParameter(TNovParamName);
            if (testParam == null) throw new Exception("Не удалось добавить обший параметр " + TNovParamName);
        }



        private ElementBinding GetBindingByParamName(String paramName, Document doc)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            DefinitionBindingMapIterator iter = doc.ParameterBindings.ForwardIterator();
            while (iter.MoveNext())
            {
                Definition curDef = iter.Key;
                if (!TNovParamName.Equals(curDef.Name)) continue;

                TNovParamDefinition = curDef;
                ElementBinding elemBind = (ElementBinding)iter.Current;
                return elemBind;
            }
            throw new Exception("не найден параметр " + paramName);
        }
    }
}