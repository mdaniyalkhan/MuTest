using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Force.DeepCloner;
using KellermanSoftware.CompareNetObjects;
using MuTest.DynamicAsserts.Properties;

namespace MuTest.DynamicAsserts
{
    public static class ObjectGraphGenerator
    {
        private const string Error = "error";
        private const string Null = "null";
        private const string StringType = "string";
        private const string DoubleType = "double";
        private const string IntegerType = "int";
        private const string UnsignedInt = "uint";
        private const string ShortType = "short";
        private const string UnsignedShort = "ushort";
        private const string DecimalType = "decimal";
        private const string FloatType = "float";
        private const string ByteType = "byte";
        private const string ShortByte = "sbyte";
        private const string Long = "long";
        private const string UnsignedLong = "ulong";
        private const string BoolType = "bool";
        private const string CharType = "char";
        private const string GuidType = "Guid";
        private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";
        private const char NullChar = '\0';
        private const string TypeMethod = ".GetType()";
        private static readonly string BaseDirectory = DynamcAssertsSettings.BaseDirectory;

        private static object _initialClass;
        private static string _testName;
        private static string _id;
        private static int _depth;
        private static bool _analyzeComplexFields;
        private static CompareLogic _compareLogic;

        private static bool _nullableType;
        private static bool _objTypeCollection;

        private static BindingFlags BindingFlags => BindingFlags.Instance |
                                                    BindingFlags.NonPublic |
                                                    BindingFlags.Public |
                                                    BindingFlags.Static;

        public static void SetupTestClass<T>(this T testClass, string testName, string id, int depth, bool compareChildren, bool deepClone) where T : new()
        {
            _initialClass = deepClone
                ? testClass.DeepClone()
                : testClass.Copy(compareChildren);
            _testName = testName;
            _id = BaseDirectory + id;
            _depth = depth;
            _analyzeComplexFields = compareChildren;

            if (!Directory.Exists(_id))
            {
                Directory.CreateDirectory(_id);
            }
        }

        public static void GenerateObjectGraphForTestClass<T>(this T modifiedClass) where T : new()
        {
            var initialComplexFieldState = _analyzeComplexFields;
            var asserts = new List<string>();
            var fieldInfos = modifiedClass.GetType().GetFields(BindingFlags);
            if (fieldInfos.Any() &&
                _initialClass != null &&
                _initialClass.GetType() == modifiedClass.GetType())
            {
                var fields = fieldInfos.Where(x => x.IsValidField()).ToList();
                var collectionFields = fields.Where(x => x.FieldType.IsACollection()).ToList();
                var simpleFields = fields.Where(x => x.FieldType.IsSimple()).ToList();

                var actionFields = fields.Where(x => x.FieldType.IsAction()).ToList();

                var complexFields = fields
                    .Except(collectionFields)
                    .Except(simpleFields)
                    .Except(actionFields);

                if (!_analyzeComplexFields)
                {
                    complexFields = complexFields.Where(x => x.FieldType.IsObject()).ToList();
                    _analyzeComplexFields = true;
                }

                // Warning: Do not change loops order
                foreach (var fieldInfo in actionFields)
                {
                    GenerateActionAssert(fieldInfo.Name, fieldInfo.GetValue(modifiedClass), asserts);
                }

                foreach (var fieldInfo in collectionFields)
                {
                    GenerateCollectionAsserts(fieldInfo.Name, fieldInfo.GetValue(modifiedClass), asserts);
                }

                foreach (var fieldInfo in simpleFields)
                {
                    GenerateSimpleMemberAsserts(fieldInfo.Name,
                        fieldInfo.GetValue(_initialClass),
                        fieldInfo.GetValue(modifiedClass),
                        fieldInfo.FieldType,
                        asserts);
                }

                foreach (var fieldInfo in complexFields)
                {
                    GenerateComplexAsserts(fieldInfo.Name,
                        fieldInfo.GetValue(_initialClass),
                        fieldInfo.GetValue(modifiedClass),
                        fieldInfo.FieldType,
                        asserts);
                }
            }

            _analyzeComplexFields = initialComplexFieldState;

            var propertiesInfo = modifiedClass.GetType().GetProperties(BindingFlags);
            if (propertiesInfo.Any() &&
                _initialClass != null &&
                _initialClass.GetType() == modifiedClass.GetType())
            {
                var properties = propertiesInfo.Where(x => x.IsValidProperty()).ToList();
                var collectionProperties = properties.Where(x => x.PropertyType.IsACollection()).ToList();
                var simpleProperties = properties.Where(x => x.PropertyType.IsSimple()).ToList();

                var actionProperties = properties.Where(x => x.PropertyType.IsAction()).ToList();

                var complexProperties = properties
                    .Except(collectionProperties)
                    .Except(simpleProperties)
                    .Except(actionProperties);

                if (!_analyzeComplexFields)
                {
                    complexProperties = complexProperties.Where(x => x.PropertyType.IsObject()).ToList();
                    _analyzeComplexFields = true;
                }

                // Warning: Do not change loops order
                foreach (var property in actionProperties)
                {
                    GenerateActionAssert(property.Name, property.GetValue(modifiedClass), asserts);
                }

                foreach (var property in collectionProperties)
                {
                    GenerateCollectionAsserts(property.Name, property.GetValue(modifiedClass), asserts);
                }

                foreach (var property in simpleProperties)
                {
                    GenerateSimpleMemberAsserts(property.Name,
                        property.GetValue(_initialClass),
                        property.GetValue(modifiedClass),
                        property.PropertyType,
                        asserts);
                }

                foreach (var property in complexProperties)
                {
                    GenerateComplexAsserts(property.Name,
                        property.GetValue(_initialClass),
                        property.GetValue(modifiedClass),
                        property.PropertyType,
                        asserts);
                }
            }

            var builder = new StringBuilder()
                .AppendLine("Name")
                .AppendLine(_testName)
                .AppendLine("Asserts");
            asserts = asserts.Distinct().ToList();
            foreach (var assert in asserts)
            {
                builder.AppendLine(assert);
            }

            var currentDateTime = DateTime.Now.Ticks;
            var path = $"{_id}/{currentDateTime}.txt";
            var file = new FileInfo(path);

            // Handling Date Time Shims
            while (file.Exists)
            {
                currentDateTime = currentDateTime + 1;
                path = $"{_id}/{currentDateTime}.txt";
                file = new FileInfo(path);
            }

            if (!file.Exists)
            {
                file.Create().Close();
            }

            File.WriteAllText(path, builder.ToString());
        }

        public static bool IsObject(this Type x)
        {
            return x == typeof(object);
        }

        public static bool IsAction(this Type x)
        {
            return x == typeof(Action);
        }

        private static bool IsValidField(this FieldInfo fieldInfo)
        {
            var fullName = fieldInfo.FieldType.FullName;
            return fullName != null &&
                   !fullName.Contains("Shim") &&
                   !fullName.Contains("IDisposable") &&
                   !fullName.Contains("PrivateType") &&
                   !fullName.Contains("DataTable") &&
                   !fullName.Contains("DataSet") &&
                   !fullName.Contains("PrivateObject") &&
                   !fullName.Contains("DropDownList") &&
                   !fullName.Contains("CultureInfo") &&
                   !fullName.Contains("RequiredFieldValidator") &&
                   !fieldInfo.Name.Contains("__BackingField") &&
                   !fieldInfo.Name.StartsWith("__") &&
                   !fieldInfo.IsLiteral &&
                   !fieldInfo.IsInitOnly;
        }

        private static bool IsValidProperty(this PropertyInfo property)
        {
            var fullName = property.PropertyType.FullName;
            return fullName != null &&
                   !fullName.Contains("Shim") &&
                   !fullName.Contains("IDisposable") &&
                   !fullName.Contains("PrivateType") &&
                   !fullName.Contains("DataTable") &&
                   !fullName.Contains("DataSet") &&
                   !fullName.Contains("PrivateObject") &&
                   !fullName.Contains("DropDownList") &&
                   !fullName.Contains("CultureInfo") &&
                   !property.Name.StartsWith("__") &&
                   property.CanRead &&
                   !fullName.Contains("RequiredFieldValidator");
        }

        private static bool Supported(this Type type)
        {
            var fullName = type.FullName;

            if (!_analyzeComplexFields)
            {
                return type.IsSimple() ||
                       type.IsACollection() ||
                       type.IsAction() ||
                       type.IsObject();
            }

            return fullName != null &&
                   !fullName.Contains("CultureInfo") &&
                   !fullName.Contains("DataSet") &&
                   !fullName.Contains("DataTable") &&
                   !fullName.Contains("DropDownList") &&
                   !fullName.Contains("IDisposable") &&
                   !fullName.Contains("Microsoft.Win32.SafeHandles.SafeFileMappingHandle") &&
                   !fullName.Contains("Microsoft.Win32.SafeHandles.SafeViewOfFileHandle") &&
                   !fullName.Contains("Microsoft.Win32.SafeHandles.SafeWaitHandle") &&
                   !fullName.Contains("PrivateObject") &&
                   !fullName.Contains("PrivateType") &&
                   !fullName.Contains("RequiredFieldValidator") &&
                   !fullName.Contains("Shim") &&
                   !fullName.Contains("System.IO.Stream") &&
                   !fullName.Contains("System.IntPtr") &&
                   !fullName.Contains("System.Reflection.INVOCATION_FLAGS") &&
                   !fullName.Contains("System.Runtime") &&
                   !fullName.Contains("System.Threading") &&
                   !fullName.Contains("System.Void*") &&
                   !fullName.Contains("log4net");
        }

        private static void GenerateComplexAsserts(string memberName, object untest, object tested, Type type, List<string> asserts)
        {
            try
            {
                _nullableType = type.IsNullable();
                if (tested != null)
                {
                    var name = memberName;
                    if (tested.GetType().IsACollection())
                    {
                        _objTypeCollection = type == typeof(object);
                        asserts.AddRange(GenerateAssertsForCollection(tested, name));
                        return;
                    }

                    untest = untest ??
                             CreateInstance(type, tested) ??
                             CreateInstance(tested.GetType(), tested);

                    if (type == typeof(object) &&
                        (!tested.GetType().IsSimple() ||
                         tested is bool ||
                         tested is string &&
                         string.IsNullOrWhiteSpace(tested.ToString())))
                    {
                        name = $"(({tested.GetType().Name.UnBoxType()}){name})";
                    }

                    asserts.AddRange(GetAsserts(untest, tested, name));
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Unsupported Field: {0}", e);
            }
        }

        private static void GenerateSimpleMemberAsserts(string memberName, object untest, object tested, Type type, List<string> asserts)
        {
            try
            {
                _nullableType = type.IsNullable();
                if (type.IsEnum)
                {
                    untest = type.GetDifferentEnum(tested) ?? untest;
                }

                if (tested != null)
                {
                    untest = untest ?? CreateInstance(type, tested);
                    asserts.AddRange(GetAsserts(untest, tested, memberName));
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Unsupported Field: {0}", e);
            }
        }

        private static void GenerateCollectionAsserts(string memberName, object objValue, List<string> asserts)
        {
            try
            {
                _nullableType = false;
                _objTypeCollection = false;
                asserts.AddRange(GenerateAssertsForCollection(objValue, memberName));
            }
            catch (Exception e)
            {
                Trace.TraceError("Unsupported Field: {0}", e);
            }
        }

        private static void GenerateActionAssert(string memberName, object objValue, ICollection<string> asserts)
        {
            if (objValue is Action action)
            {
                try
                {
                    action.Invoke();
                    asserts.Add($"() => {memberName}.ShouldNotThrow(),-");
                }
                catch (Exception e)
                {
                    var replace = e.Message.FormatMultiLines();
                    asserts.Add($"() => {memberName}.ShouldThrow<{e.GetType().Name}>().Message.ShouldBe(\"{replace}\"),-string");
                }
            }
        }

        private static string FormatMultiLines(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            return str
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }

        public static bool IsSimple(this Type x)
        {
            return x.IsNumber() ||
                   x.IsBoolean() ||
                   x.IsText() ||
                   x.IsDateTime() ||
                   x.IsEnum;
        }

        private static IList<string> GenerateAssertsForCollection(object list, string fieldName)
        {
            var asserts = new List<string>();
            try
            {
                dynamic obj = list;
                var index = 0;
                var allNullElements = true;

                if (_objTypeCollection)
                {
                    fieldName = list.GetType().IsArray
                        ? $"(({list.GetType().GetElementType()?.Name.UnBoxType()}[]){fieldName})"
                        : $"((List<{list.GetType().GetGenericArguments()[0].Name.UnBoxType()}>){fieldName})";
                }

                foreach (var element in obj)
                {
                    if (element != null)
                    {
                        var formattedField = $"{fieldName}[{index}]";
                        asserts.AddRange(GetAsserts(CreateInstance(element.GetType(), null),
                            element,
                            formattedField));
                        allNullElements = false;
                    }

                    index++;
                }

                if (!allNullElements)
                {
                    if (index == 1)
                    {
                        asserts.Add($"() => {fieldName}.ShouldHaveSingleItem(),-");
                    }

                    if (index > 1)
                    {
                        asserts.Add(list.GetType().IsArray
                            ? $"() => {fieldName}.Length.ShouldBeGreaterThanOrEqualTo({index}),-"
                            : $"() => {fieldName}.Count.ShouldBeGreaterThanOrEqualTo({index}),-");
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Unsupported Type :{0}", e);
            }

            return asserts;
        }

        private static object CreateInstance(Type type, object tested)
        {
            try
            {
                if (type == typeof(object))
                {
                    return null;
                }

                var instance = GetDefault(type, tested);

                if (instance == null)
                {
                    instance = CreateInstanceOfTypeHavingDefaultConstructor(type);
                }

                if (instance == null)
                {
                    instance = CreateUninitializedObject(type);
                }

                return instance;
            }
            catch (Exception e)
            {
                Trace.TraceError("Unsupported Type {0}", e);
            }


            return null;
        }

        private static dynamic CreateUninitializedObject(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            dynamic instance = null;

            try
            {
                instance = FormatterServices.GetUninitializedObject(type);
            }
            catch (Exception exp)
            {
                Trace.TraceError("Failed to create instance of type: {0}", exp);
            }

            return instance;
        }

        private static dynamic CreateInstanceOfTypeHavingDefaultConstructor(this Type type)
        {
            return CreateInstanceOfTypeHavingPublicDefaultConstructor(type) ??
                   CreateInstanceOfTypeHavingPrivateDefaultConstructor(type);
        }

        private static bool IsPublicDefaultConstructorExist(this Type classType)
        {
            var constructorInfo = classType.GetConstructor(Type.EmptyTypes);

            return constructorInfo != null && constructorInfo.IsPublic;
        }

        private static dynamic CreateInstanceOfTypeHavingPrivateDefaultConstructor(this Type type)
        {
            dynamic instance = null;
            try
            {
                instance = Activator.CreateInstance(type, BindingFlags, null, new object[] { }, null);
            }
            catch (Exception exp)
            {
                Trace.TraceError("Failed to create instance of type: {0}", exp);
            }

            return instance;
        }

        private static dynamic CreateInstanceOfTypeHavingPublicDefaultConstructor(this Type type)
        {
            dynamic instance = null;
            if (IsPublicDefaultConstructorExist(type))
            {
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (Exception exp)
                {
                    Trace.TraceError("Failed to create instance of type: {0}", exp);
                }
            }

            return instance;
        }

        private static IList<string> GetAsserts(object untest, object tested, string fieldName)
        {
            var asserts = new List<string>();
            if (untest != null)
            {
                _compareLogic = new CompareLogic
                {
                    Config = new ComparisonConfig
                    {
                        MaxDifferences = int.MaxValue,
                        MaxStructDepth = _depth,
                        CompareChildren = _analyzeComplexFields,
                        CustomComparers =
                        {
                            new ColorComparer(RootComparerFactory.GetRootComparer())
                        }
                    }
                };

                var generateGraph = GenerateGraph(untest, tested, fieldName);
                if (generateGraph.Any(x => x == Error))
                {
                    _compareLogic.Config.CompareChildren = false;
                    generateGraph = GenerateGraph(untest, tested, fieldName);
                }

                if (generateGraph.All(x => x != Error))
                {
                    asserts.AddRange(generateGraph);
                }
            }

            return asserts;
        }

        private static IList<string> GenerateGraph(object obj, object actual, string fieldName)
        {
            var asserts = new List<string>();
            try
            {
                var comparisonResult = _compareLogic.Compare(obj, actual);
                foreach (var difference in comparisonResult.Differences)
                {
                    try
                    {
                        asserts.Add(GetAssert(difference, fieldName));
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Unable to generate asserts {0}", e);
                    }
                }
            }
            catch (Exception e)
            {
                asserts.Clear();
                asserts.Add(Error);
                Trace.TraceError("Unable to compare object {0}", e);
            }

            return asserts;
        }

        private static string GetAssert(this Difference difference, string fieldName)
        {
            try
            {
                if (difference.Object1 != null && !difference.Object1.GetType().Supported())
                {
                    return null;
                }

                if (difference.Object2 != null && !difference.Object2.GetType().Supported())
                {
                    return null;
                }

                if (difference.ParentObject2 != null)
                {
                    var propertyInfo = difference.ParentObject2.GetType().GetProperty(difference.PropertyName);
                    propertyInfo?.GetValue(difference.ParentObject2);
                }
            }
            catch (Exception)
            {
                return null;
            }

            if (difference.ParentObject2 != null &&
                difference.ParentObject2.GetType().Name.StartsWith("Tuple`"))
            {
                var output = new StringBuilder("Tuple.Create(");
                var fields = difference.ParentObject2.GetType().GetFields(BindingFlags);
                for (var index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];
                    output.Append(GetTupleValue(field.FieldType.Name, field.GetValue(difference.ParentObject2)));

                    if (index < fields.Length - 1)
                    {
                        output.Append(", ");
                    }
                }

                output = output.Append(")");

                return $"() => {fieldName}.ShouldBe({output}),-Tuple";
            }

            if (difference.ParentObject2 != null)
            {
                var propertyInfo = difference.ParentObject2.GetType().GetProperty(difference.PropertyName);
                if (propertyInfo != null && propertyInfo.PropertyType.IsNullable())
                {
                    _nullableType = true;
                }
                else
                {
                    _nullableType = false;
                }
            }

            var type = difference.Object2TypeName != Null
                ? difference.Object2TypeName
                : difference.Object1TypeName;
            var propertyName = string.IsNullOrWhiteSpace(difference.PropertyName)
                ? string.Empty
                : $"{difference.PropertyName}.";

            var childProperty = difference.ChildPropertyName;
            if (childProperty == "GetType()")
            {
                difference.ChildPropertyName = null;
            }

            if (!string.IsNullOrWhiteSpace(difference.ChildPropertyName))
            {
                propertyName = string.IsNullOrWhiteSpace(propertyName)
                    ? $"{difference.ChildPropertyName}."
                    : $"{propertyName}{difference.ChildPropertyName}.";
            }

            propertyName = propertyName.Replace(TypeMethod, string.Empty);
            fieldName = fieldName.Replace(TypeMethod, string.Empty);

            var unBoxType = type.UnBoxType();
            if ((difference.Object2 == null || string.IsNullOrWhiteSpace(difference.Object2Value)) && unBoxType == StringType)
            {
                var nested = string.IsNullOrWhiteSpace(difference.PropertyName)
                    ? string.Empty
                    : $"{difference.PropertyName}.";
                return $"() => {fieldName}.{nested}ShouldBeNullOrWhiteSpace(),-{unBoxType}";
            }

            if (difference.Object2 == null)
            {
                var nested = string.IsNullOrWhiteSpace(difference.PropertyName)
                    ? string.Empty
                    : $"{difference.PropertyName}.";
                return $"() => {fieldName}.{nested}ShouldBeNull(),-{unBoxType}";
            }

            if (childProperty == "GetType()")
            {
                unBoxType = difference.Object2.GetType().Name.UnBoxType();
                difference.Object2Value = difference.Object2.ToString();
            }

            if (unBoxType != StringType &&
                unBoxType != UnsignedInt &&
                unBoxType != ShortType &&
                unBoxType != UnsignedShort &&
                unBoxType != ByteType &&
                unBoxType != ShortByte &&
                unBoxType != UnsignedLong &&
                unBoxType != CharType)
            {
                unBoxType = difference.Object2Value.IsNumeric()
                    ? difference.Object2Value.IsInteger()
                        ? IntegerType
                        : DoubleType
                    : unBoxType;
            }

            var enumType = CheckEnum(difference, fieldName, propertyName);
            if (!string.IsNullOrWhiteSpace(enumType))
            {
                return enumType;
            }

            if (difference.Object2.GetType().IsACollection())
            {
                var collectionProperty = propertyName
                    .Replace(".Count", string.Empty)
                    .Replace(".Length", string.Empty)
                    .Replace(TypeMethod, string.Empty);
                _objTypeCollection = false;
                var generateAssertsForCollection = GenerateAssertsForCollection(difference.Object2, $"{fieldName}.{collectionProperty}".Trim('.'));

                var builder = new StringBuilder();
                for (var index = 0; index < generateAssertsForCollection.Count; index++)
                {
                    var assert = generateAssertsForCollection[index];

                    if (index != generateAssertsForCollection.Count - 1)
                    {
                        builder.AppendLine(assert);
                    }
                    else
                    {
                        builder.Append(assert);
                    }
                }

                return builder.ToString();
            }

            var nullableType = _nullableType
                ? "GetValueOrDefault()."
                : string.Empty;

            switch (unBoxType)
            {
                case IntegerType:
                case DoubleType:
                case DecimalType:
                case FloatType:
                case Long:
                    return $"() => {fieldName}.{propertyName}{nullableType}ShouldBe({difference.Object2Value}),-{unBoxType}";
                case UnsignedInt:
                case ShortType:
                case UnsignedShort:
                case ByteType:
                case ShortByte:
                case UnsignedLong:
                    return $"() => {fieldName}.{propertyName}{nullableType}ShouldBe(({unBoxType}){difference.Object2Value}),-{unBoxType}";
                case BoolType:
                    return difference.Object2Value.Equals("false", StringComparison.InvariantCultureIgnoreCase)
                        ? $"() => {fieldName}.{propertyName}{nullableType}ShouldBeFalse(),-{unBoxType}"
                        : $"() => {fieldName}.{propertyName}{nullableType}ShouldBeTrue(),-{unBoxType}";
                case CharType:
                    return $"() => {fieldName}.{propertyName}{nullableType}ShouldBe('{difference.Object2Value}'),-{unBoxType}";
                case StringType:
                    var replace = difference.Object2Value.FormatMultiLines();
                    return $"() => {fieldName}.{propertyName}ShouldBe(\"{replace}\"),-{unBoxType}";
                case GuidType:
                    return difference.Object2Value.Equals(EmptyGuid)
                        ? $"() => {fieldName}.{propertyName}{nullableType}ShouldBe(Guid.Empty),-{unBoxType}"
                        : $"() => {fieldName}.{propertyName}{nullableType}ShouldBe(new Guid(\"{difference.Object2Value}\")),-{unBoxType}";
                case "DateTime":
                    var dateTime = (DateTime)difference.Object2;
                    return $"() => {fieldName}.{propertyName}{nullableType}ToString(CultureInfo.InvariantCulture).ShouldBe(\"{dateTime.ToString(CultureInfo.InvariantCulture)}\"),-{unBoxType}";
                default:
                    if (difference.Object2 != null && 
                        difference.Object2.GetType().FullName == "System.Drawing.Color")
                    {
                        var colorName = difference.Object2.GetType().GetProperty("Name").GetValue(difference.Object2);
                        return
                            $"() => {fieldName}.{propertyName.Replace(TypeMethod, string.Empty)}Name.ShouldBe(\"{colorName}\"),-{unBoxType}";
                    }


                    if (!string.IsNullOrWhiteSpace(unBoxType))
                    {
                        var unknownBuilder = new StringBuilder()
                            .AppendLine($"() => {fieldName}.{propertyName.Replace(TypeMethod, string.Empty)}ShouldNotBeNull(),-{unBoxType}")
                            .Append($"() => {fieldName}.{propertyName.Replace(TypeMethod, string.Empty)}ShouldBeOfType<{unBoxType}>(),-{unBoxType}");
                        return unknownBuilder.ToString();
                    }

                    return null;
            }
        }

        private static string GetTupleValue(string type, object typeValue)
        {
            var unBoxType = type.UnBoxType();
            if (typeValue == null)
            {
                return $"({unBoxType})null";
            }

            if (typeValue.GetType().IsEnum)
            {
                return $"{typeValue.GetType().Name}.{typeValue}";
            }

            if (typeValue.GetType().Name.Equals("RuntimeType", StringComparison.InvariantCultureIgnoreCase))
            {
                return $"typeof({((Type)typeValue).Name})";
            }

            var stringValue = typeValue.ToString();
            switch (unBoxType)
            {
                case IntegerType:
                    return stringValue;
                case DoubleType:
                case DecimalType:
                case FloatType:
                case Long:
                case UnsignedInt:
                case ShortType:
                case UnsignedShort:
                case ByteType:
                case ShortByte:
                case UnsignedLong:
                    return $"({unBoxType}){stringValue}";
                case BoolType:
                    return stringValue.Equals("false", StringComparison.InvariantCultureIgnoreCase)
                        ? "false"
                        : "true";
                case CharType:
                    return $"'{stringValue}'";
                case StringType:
                    var replace = stringValue.FormatMultiLines();
                    return $"\"{replace}\"";
                case GuidType:
                    return stringValue.Equals(EmptyGuid)
                        ? "Guid.Empty"
                        : $"new Guid(\"{stringValue}\")";
                default:
                    return $"\"{stringValue}\"";
            }
        }

        private static string CheckEnum(Difference difference, string fieldName, string propertyName)
        {
            var enumObject = difference.ParentObject2 ?? difference.Object2;
            var propertyToCheck = difference.ParentObject2 == null
                ? difference.Object2TypeName
                : difference.PropertyName;
            if (enumObject != null && !string.IsNullOrWhiteSpace(propertyToCheck))
            {
                var propertyInfo = enumObject
                    .GetType()
                    .GetProperty(propertyToCheck.Split('.').Last());

                if (propertyInfo != null && propertyInfo.PropertyType.IsEnum || enumObject.GetType().IsEnum)
                {
                    var enumValue = difference.Object2Value;
                    if (enumValue.IsNumeric())
                    {
                        if (propertyInfo != null && propertyInfo.PropertyType.IsEnum)
                        {
                            enumValue = propertyInfo.PropertyType.GetEnumAtSpecificIndex(Convert.ToInt32(enumValue)).ToString();
                        }
                        else if (enumObject.GetType().IsEnum)
                        {
                            enumValue = enumObject.GetType().GetEnumAtSpecificIndex(Convert.ToInt32(enumValue)).ToString();
                        }
                    }

                    if (!enumValue.IsNumeric())
                    {
                        return $"() => {fieldName}.{propertyName}ShouldBe({difference.Object2TypeName}.{enumValue}),-enum";
                    }

                    return $"() => {fieldName}.{propertyName}ShouldBe(({difference.Object2TypeName}){enumValue}),-enum";
                }
            }

            return null;
        }

        private static string UnBoxType(this string type)
        {
            switch (type.Trim('?'))
            {
                case "Int32":
                    return IntegerType;
                case "UInt32":
                    return UnsignedInt;
                case "Double":
                    return DoubleType;
                case "Int16":
                    return ShortType;
                case "UInt16":
                    return UnsignedShort;
                case "Decimal":
                    return DecimalType;
                case "Float":
                    return FloatType;
                case "Byte":
                    return ByteType;
                case "SByte":
                    return ShortByte;
                case "Int64":
                    return Long;
                case "UInt64":
                    return UnsignedLong;
                case "Boolean":
                    return BoolType;
                case "Char":
                    return CharType;
                case "String":
                    return StringType;
                default:
                    return type;
            }
        }

        private static dynamic GetDefault(this Type type, object tested)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsNumber())
            {
                if (tested == null || !Activator.CreateInstance(type).Equals(tested))
                {
                    return Convert.ChangeType("0", type);
                }

                return Convert.ChangeType("1", type);
            }

            if (type == typeof(char) || type == typeof(char?))
            {
                if (tested == null || !Convert.ToChar(tested).Equals(NullChar))
                {
                    return NullChar;
                }

                return 't';
            }

            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                return DateTime.MinValue;
            }

            if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
            {
                return DateTimeOffset.MinValue;
            }

            if (type == typeof(string))
            {
                if (tested == null || string.IsNullOrWhiteSpace(tested.ToString()))
                {
                    return "test-string";
                }

                return string.Empty;
            }

            if (type == typeof(bool) || type == typeof(bool?))
            {
                return !Convert.ToBoolean(tested);
            }

            if (type == typeof(Guid) || type == typeof(Guid?))
            {
                return Guid.Empty;
            }

            return null;
        }

        private static object GetDifferentEnum(this Type enumType, object current)
        {
            var enumValues = Enum.GetValues(enumType);
            for (var index = 0; index < enumValues.Length; index++)
            {
                object enumValue = enumValues.GetValue(index);
                if (!enumValue.ToString().Equals(current.ToString()))
                {
                    return enumValue;
                }
            }

            return null;
        }

        private static object GetEnumAtSpecificIndex(this Type enumType, int enumIndex)
        {
            var enumValues = Enum.GetValues(enumType);
            for (var index = 0; index < enumValues.Length; index++)
            {
                object enumValue = enumValues.GetValue(index);
                if (Convert.ToInt32(enumValue) == enumIndex)
                {
                    return enumValue;
                }
            }

            return enumIndex.ToString();
        }

        private static bool IsNumeric(this string text)
        {
            return double.TryParse(text, out _);
        }

        private static bool IsInteger(this string text)
        {
            if (!text.IsNumeric() ||
                text.Contains(".") ||
                Convert.ToDouble(text) > int.MaxValue)
            {
                return false;
            }

            return int.TryParse(text, out _);
        }

        public static bool IsACollection(this Type type)
        {
            return type != typeof(string) &&
                   type.GetInterface(nameof(IEnumerable)) != null ||
                   type.GetInterface(nameof(ICollection)) != null ||
                   type.GetInterface(nameof(IList)) != null ||
                   type.IsArray;
        }

        private static bool IsNumber(this Type type)
        {
            return type == typeof(sbyte) ||
                   type == typeof(sbyte?) ||
                   type == typeof(byte?) ||
                   type == typeof(byte) ||
                   type == typeof(short) ||
                   type == typeof(short?) ||
                   type == typeof(ushort) ||
                   type == typeof(ushort?) ||
                   type == typeof(int) ||
                   type == typeof(int?) ||
                   type == typeof(uint) ||
                   type == typeof(uint?) ||
                   type == typeof(long) ||
                   type == typeof(long?) ||
                   type == typeof(ulong) ||
                   type == typeof(ulong?) ||
                   type == typeof(double) ||
                   type == typeof(double?) ||
                   type == typeof(float) ||
                   type == typeof(float?) ||
                   type == typeof(decimal) ||
                   type == typeof(decimal?);
        }

        private static bool IsText(this Type type)
        {
            return type == typeof(string) ||
                   type == typeof(char?) ||
                   type == typeof(char);
        }

        private static bool IsBoolean(this Type type)
        {
            return type == typeof(bool) ||
                   type == typeof(bool?);
        }

        private static bool IsDateTime(this Type type)
        {
            return type == typeof(DateTime) ||
                   type == typeof(DateTime?);
        }

        private static bool IsNullable(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }
    }
}