﻿// MIT License
// 
// Copyright (c) 2016 Wojciech Nagórski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace ExtendedXmlSerialization.Cache
{
    internal class TypeDefinition
    { 
        public TypeDefinition(Type type)
        {
            Type = type;
            Name = type.Name;

            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsGenericType)
            {
                Type[] types = type.GetGenericArguments();

                if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    Type = type.GetGenericArguments()[0];
                }

                Name = Name.Replace("`" + types.Length, "Of" + string.Join("", types.Select(p => p.Name)));
            }
            
            FullName = type.FullName;

            GetPrimitiveInfo(Type);

            IsArray = typeInfo.IsArray;

            if (!IsPrimitive && typeof(IEnumerable).IsAssignableFrom(type))
            {

                IsEnumerable = true;
            }

            if (IsEnumerable)
            {
                if (typeof(IDictionary).IsAssignableFrom(type))
                {
                    IsDictionary = true;
                }
                var elementType = type.GetElementType();
                if (elementType != null)
                {
                    Name = "ArrayOf" + elementType.Name;
                }

                if (typeInfo.IsGenericType)
                {
                    GenericArguments = type.GetGenericArguments();
                    if (elementType == null)
                    {
                        Name = "ArrayOf" + string.Join("", GenericArguments.Select(p => p.Name));
                    }
                    if (IsDictionary)
                    {
                        MethodAddToDictionary = ObjectAccessors.CreateMethodAddToDictionary(type);
                    }
                    else
                    {
                        MethodAddToCollection = ObjectAccessors.CreateMethodAddCollection(type);
                    }
                }
            }


            IsObjectToSerialize = // !typeInfo.IsPrimitive && !typeInfo.IsValueType &&
                !IsPrimitive &&
                !typeInfo.IsEnum && type != typeof(string) &&
                //not generic or generic but not List<> and Set<>
                (!typeInfo.IsGenericType ||
                 (typeInfo.IsGenericType && !typeof(IEnumerable).IsAssignableFrom(type)));
            if (IsObjectToSerialize)
            {
                Properties = GetPropertieToSerialze(type);
            }
            
            ObjectActivator = ObjectAccessors.CreateObjectActivator(type, IsPrimitive);
        }

        public ObjectAccessors.AddItemToCollection MethodAddToCollection { get; set; }
        public ObjectAccessors.AddItemToDictionary MethodAddToDictionary { get; set; }


        private static List<PropertieDefinition> GetPropertieToSerialze(Type type)
        {
            var result = new List<PropertieDefinition>();
            var properties = type.GetProperties();
            foreach (PropertyInfo propertyInfo in properties)
            {
                var getMethod = propertyInfo.GetGetMethod(true);
                if (!propertyInfo.CanRead || getMethod.IsStatic || !getMethod.IsPublic ||
                    !propertyInfo.CanWrite || !propertyInfo.GetSetMethod(true).IsPublic ||
                    propertyInfo.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                bool ignore = propertyInfo.GetCustomAttributes(false).Any(a => a is XmlIgnoreAttribute);
                if (ignore)
                {
                    continue;
                }

                result.Add(new PropertieDefinition(type, propertyInfo));
            }

            var fields = type.GetFields();
            foreach (FieldInfo field in fields)
            {
                if (field.IsLiteral && !field.IsInitOnly)
                {
                    continue;
                }
                if (field.IsInitOnly || field.IsStatic)
                {
                    continue;
                }
                bool ignore = field.GetCustomAttributes(false).Any(a => a is XmlIgnoreAttribute);
                if (ignore)
                {
                    continue;
                }

                result.Add(new PropertieDefinition(type, field));
            }
            return result;
        }

        public bool IsPrimitive { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsEnumerable { get; private set; }
        public bool IsDictionary { get; private set; }
        public Type[] GenericArguments { get; set; }

        public List<PropertieDefinition> Properties { get; private set; }
        public Type Type { get; private set; }
        public string Name { get; private set; }
        public string FullName { get; private set; }
        public bool IsObjectToSerialize { get; private set; }
        public bool IsEnum { get; private set; }

        public string PrimitiveName { get; private set; }
        public TypeCode TypeCode { get; set; }
        public ObjectAccessors.ObjectActivator ObjectActivator { get; private set; }

        public PropertieDefinition GetProperty(string name)
        {
            return Properties.FirstOrDefault(p => p.Name == name);
        }

        private void GetPrimitiveInfo(Type type)
        {
            IsEnum = type.GetTypeInfo().IsEnum;

            TypeCode = Type.GetTypeCode(type);

            switch (TypeCode)
            {
                case TypeCode.Boolean:
                    PrimitiveName = "boolean";
                    break;
                case TypeCode.Char:
                    PrimitiveName = "char";
                    break;
                case TypeCode.SByte:
                    PrimitiveName = "byte";
                    break;
                case TypeCode.Byte:
                    PrimitiveName = "unsignedByte";
                    break;
                case TypeCode.Int16:
                    PrimitiveName = "short";
                    break;
                case TypeCode.UInt16:
                    PrimitiveName = "unsignedShort";
                    break;
                case TypeCode.Int32:
                    PrimitiveName = "int";
                    break;
                case TypeCode.UInt32:
                    PrimitiveName = "unsignedInt";
                    break;
                case TypeCode.Int64:
                    PrimitiveName = "long";
                    break;
                case TypeCode.UInt64:
                    PrimitiveName = "unsignedLong";
                    break;
                case TypeCode.Single:
                    PrimitiveName = "float";
                    break;
                case TypeCode.Double:
                    PrimitiveName = "double";
                    break;
                case TypeCode.Decimal:
                    PrimitiveName = "decimal";
                    break;
                case TypeCode.DateTime:
                    PrimitiveName = "dateTime";
                    break;
                case TypeCode.String:
                    PrimitiveName = "string";
                    break;
                default:
                    if (type == typeof(Guid))
                    {
                        PrimitiveName = "guid";

                        break;
                    }
                    if (type == typeof(TimeSpan))
                    {
                        PrimitiveName = "TimeSpan";
                        break;
                    }

                    break;
            }
            IsPrimitive = !string.IsNullOrEmpty(PrimitiveName);          
        }
    }
}
