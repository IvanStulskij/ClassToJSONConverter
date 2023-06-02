using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IspierTestTask
{
    public class LanguageConverter
    {
        public async Task ClassToJSON(string className)
        {
            ClassInfo classInfo = GetClassInfo(className);
            string json = await JsonContent.Create(classInfo).ReadAsStringAsync();

            File.WriteAllText($"{className}.json", json);
        }

        public void XMLToClass()
        {
            XDocument doc = XDocument.Load("data_class.xml");
            XElement root = doc.Element("sql_script");
            var className = root.Descendants().FirstOrDefault(x => x.Name == "tableview_name").Value;
            var value = className;
            value = value.Insert(0, "public class ");
            value += "\n{\n";
            IEnumerable<string> dataTypes = root.Descendants().Where(x => x.Name == "native_datatype_element").Select(x => x.Value);
            IEnumerable<string> fieldsNames = root.Descendants().Where(x => x.Name == "column_name").Select(x => x.Value);
            
            for (int fieldIndex = 0; fieldIndex < dataTypes.Count(); fieldIndex++)
            {
                value += ConfigurePropertyString(dataTypes.ElementAt(fieldIndex), fieldsNames.ElementAt(fieldIndex));
            }
            value += "}\n";

            File.WriteAllText($"{className}.cs", value);
        }

        private ClassInfo GetClassInfo(string className)
        {
            var classText = File.ReadAllText($"{className}.cs");
            var properties = GetProperties(classText);
            var fields = GetFields(classText);
            var methods = GetMethods(classText);
            var constants = GetConstants(classText);

            return new ClassInfo
            {
                ClassName = className,
                Fields = ConfigureFieldsAsJSONInfo(fields),
                Properties = ConfigurePropertiesAsJSONInfo(properties),
                Constants = ConfigureConstantsAsJSONInfo(constants),
                Methods = ConfigureMethodsAsJSONInfo(methods),
                Parents = GetParents(classText),
                ConstructorInfos = GetConstructors(classText, className),
            };
        }

        private IEnumerable<PropertyInfo> ConfigureFieldsAsJSONInfo(IEnumerable<string> fields)
        {
            return fields.Select(x =>
            {
                var splittedProperty = Regex.Split(x, @"\s+");
                var accessModifier = splittedProperty.First();
                var fieldType = splittedProperty[1];
                var fieldName = splittedProperty.Last().Replace(";", string.Empty);

                return new PropertyInfo()
                {
                    AccessModifier = accessModifier,
                    PropertyInfo = fieldName,
                    PropertyType = fieldType
                };
            });
        }

        private IEnumerable<PropertyInfo> ConfigurePropertiesAsJSONInfo(IEnumerable<string> properties)
        {
            return properties.Select(x =>
            {
                var splittedProperty = Regex.Split(x, @"\s+");
                var accessModifier = splittedProperty.First();
                var propertyType = splittedProperty[1];
                var propertyName = splittedProperty.Last();

                return new PropertyInfo()
                {
                    AccessModifier = accessModifier,
                    PropertyInfo = propertyName,
                    PropertyType = propertyType
                };
            });
        }

        private IEnumerable<MethodInfo> ConfigureMethodsAsJSONInfo(IEnumerable<string> methods)
        {
            return methods.Select(x =>
            {
                var splittedProperty = Regex.Split(x, @"\s+|\(");
                var accessModifier = splittedProperty.First();
                var propertyType = splittedProperty[1];
                var propertyName = splittedProperty[2];
                var parameters = x.Replace(accessModifier, string.Empty).Replace(propertyName, string.Empty).Replace(propertyType, string.Empty).Replace("(", string.Empty).Trim();

                return new MethodInfo
                {
                    AccessModifier = accessModifier,
                    Name = propertyName,
                    ReturnedType = propertyType,
                    Parameters = parameters.Split(",").Select(x =>
                    {
                        var splittedProperty = Regex.Split(x, @"\s+");

                        var paramName = splittedProperty.Last();
                        var paramType = splittedProperty.First();

                        return new VariableInfo()
                        {
                            PropertyInfo = paramName,
                            PropertyType = paramType,
                        };
                    })
                };
            });
        }

        private IEnumerable<ConstInfo> ConfigureConstantsAsJSONInfo(IEnumerable<string> constants)
        {
            return constants.Select(x =>
            {
                var splittedProperty = Regex.Split(x, @"\s+");
                var accessModifier = splittedProperty.First();
                var propertyType = splittedProperty[1];
                var propertyName = splittedProperty[2];
                var value = x.Replace(accessModifier, string.Empty).Replace(propertyName, string.Empty).Replace(propertyType, string.Empty).Replace("\"", string.Empty).Trim();

                return new ConstInfo
                {
                    AccessModifier = accessModifier,
                    PropertyInfo = propertyName,
                    PropertyType = propertyType,
                    Value = value
                };
            });
        }

        private IEnumerable<CtorInfo> GetConstructors(string classText, string className)
        {
            return Regex.Matches(classText, "(public|private|protected) *[a-zA-Zа-яА-Я]* *\\(([a-zA-Zа-яА-Я ,*])*")
                .Select(x => x.Value.Replace(className, string.Empty).Replace("(", string.Empty))
                .Select(x => Regex.Replace(x, "(public|private|protected)", string.Empty))
                .Select(x =>
                {
                    var ctorsConfigurations = Regex.Split(x, @",");


                    var properties = ctorsConfigurations.Where(x => !string.IsNullOrWhiteSpace(x)).Select(ctorConfiguration =>
                    {
                        var propertyInfo = Regex.Split(ctorConfiguration.Trim(), @"\s+");

                        return new VariableInfo
                        {
                            PropertyType = propertyInfo[0],
                            PropertyInfo = propertyInfo[1],
                        };
                    });

                    return new CtorInfo()
                    {
                        Properties = properties,
                    };
                });
        }

        private IEnumerable<string> GetFields(string classText) => Regex.Matches(classText, "(public|private|protected) [a-zA-Zа-яА-Я _]* *;")
            .Select(x => x.Value);

        private IEnumerable<string> GetParents(string classText)
        {
            var classConfiguration = Regex.Match(classText, "(public|private|protected) *class * [a-zA-Zа-яА-Я _\r\n]* *:[a-zA-Zа-яА-Я _,\r\n]*").Value;
            if (!string.IsNullOrWhiteSpace(classConfiguration))
            {
                classConfiguration = classConfiguration
                    .Substring((classConfiguration.IndexOf(":")) - 1, classConfiguration.Length - classConfiguration.IndexOf(":"))
                    .Replace("\r\n", string.Empty)
                    .Replace(":", string.Empty)
                    .Trim();
            }

            return classConfiguration.Split(",").Select(x => x.Trim());
        }

        private IEnumerable<string> GetProperties(string classText) => Regex.Matches(classText, "(public|private|protected) [a-zA-Zа-яА-Я ]* * { *get; *set; *}")
            .Select(x => Regex.Replace(x.Value, "{ *get; *set; *}", string.Empty).Trim());

        private IEnumerable<string> GetMethods(string classText) => Regex.Matches(classText, @"(public|private|protected) *[a-zA-Zа-яА-Я]+ [a-zA-Zа-яА-Я]* *\(([a-zA-Zа-яА-Я ,*])*")
            .Select(x => x.Value);

        private IEnumerable<string> GetConstants(string classText) => Regex.Matches(classText, @"(public|private|protected) *const * [a-zA-Zа-яА-Я _]* *=[a-zA-Zа-яА-Я _""]*;")
            .Select(x => x.Value.Replace("const", string.Empty).Replace("=", string.Empty).Replace(";", string.Empty).Trim());

        private string ConfigurePropertyString(string dataType, string fieldName) => $"\tpublic {ConvertedDataTypes.DataTypes[dataType]} {fieldName} " + "{ get; set; }\n";
    }

    public class ClassInfo
    {
        public string ClassName { get; set; }

        public IEnumerable<PropertyInfo> Fields { get; set; } = new List<PropertyInfo>();

        public IEnumerable<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();

        public IEnumerable<MethodInfo> Methods { get; set; } = new List<MethodInfo>();

        public IEnumerable<CtorInfo> ConstructorInfos { get; set; } = new List<CtorInfo>();

        public IEnumerable<ConstInfo> Constants { get; set; } = new List<ConstInfo>();

        public IEnumerable<string> Parents { get; set; } = new List<string>();
    }

    public class MethodInfo
    {
        public MethodInfo()
        {
        }

        public string AccessModifier { get; set; }

        public string Name { get; set; }

        public string ReturnedType { get; set; }

        public IEnumerable<VariableInfo> Parameters { get; set; } = new List<VariableInfo>();
    }

    public class CtorInfo
    {
        public IEnumerable<VariableInfo> Properties { get; set; } = new List<VariableInfo>(); 
    }

    public class VariableInfo
    {
        public string PropertyInfo { get; set; }

        public string PropertyType { get; set; }
    }

    public class PropertyInfo : VariableInfo
    {
        public string AccessModifier { get; set; }
    }

    public class ConstInfo : PropertyInfo
    {
        public string Value { get; set; }
    }

    public static class ConvertedDataTypes
    {
        public static Dictionary<string, string> DataTypes = new Dictionary<string, string>()
        {
            { "NUMBER", "int" },
            { "TEXT", "string" },
            { "DATE", "DateTime" },
            { "BIT", "boolean" }
        };
    }
}
