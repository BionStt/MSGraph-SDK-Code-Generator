// Copyright (c) Microsoft Open Technologies, Inc. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the source repository root for license information.﻿

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Vipr.T4TemplateWriter.Settings;
using Vipr.Core.CodeModel;

namespace Vipr.T4TemplateWriter
{
    public static class OdcmModelExtensions
    {
        public static bool IsCollection(this OdcmProperty odcmProperty)
        {
            return odcmProperty.IsCollection;
        }


        private static OdcmNamespace GetOdcmNamespace(OdcmModel model)
        {
            OdcmNamespace namespaceFound;
            var filtered = model.Namespaces.Where(x => !x.Name.Equals("Edm", StringComparison.InvariantCultureIgnoreCase))
                                           .ToList();
            if (filtered.Count() == 1)
            {
                namespaceFound = filtered.Single();
            }
            else
            {
                namespaceFound =
                    model.Namespaces.Find(x => String.Equals(x.Name, ConfigurationService.Settings.PrimaryNamespaceName,
                        StringComparison.InvariantCultureIgnoreCase));
            }

            if (namespaceFound == null)
            {
                throw new InvalidOperationException("Multiple namespaces defined in metadata and no matches." +
                                                    "\nPlease check 'PrimaryNamespace' Setting in 'config.json'");
            }
            return namespaceFound;
        }

        public static IEnumerable<OdcmClass> GetComplexTypes(this OdcmModel model)
        {
            var @namespace = GetOdcmNamespace(model);
            return @namespace.Classes.Where(x => x.Kind == OdcmClassKind.Complex);
        }

        public static IEnumerable<OdcmClass> GetEntityTypes(this OdcmModel model)
        {
            var @namespace = GetOdcmNamespace(model);
            return @namespace.Classes.Where(x => x.Kind == OdcmClassKind.Entity || x.Kind == OdcmClassKind.MediaEntity);
        }

        public static IEnumerable<OdcmProperty> GetProperties(this OdcmModel model)
        {
            return model.GetEntityTypes().SelectMany(entityTypes => entityTypes.Properties)
                        .Union(model.EntityContainer.Properties)
                        .Union(model.GetComplexTypes().SelectMany(complexType => complexType.Properties));
        }

        public static IEnumerable<OdcmProperty> GetPropertyType(this OdcmClass entity, string propertyTypeName)
        {
            return entity.Properties.Where(prop => prop.Type.Name.Equals(propertyTypeName));
        }

        public static IEnumerable<OdcmEnum> GetEnumTypes(this OdcmModel model)
        {
            var @namespace = GetOdcmNamespace(model);
            return @namespace.Types.OfType<OdcmEnum>();
        }

        public static IEnumerable<OdcmMethod> GetMethods(this OdcmModel model)
        {
            return model.GetEntityTypes().SelectMany(entityType => entityType.Methods);
        }

        public static IEnumerable<OdcmProperty> NavigationProperties(this OdcmClass odcmClass)
        {
            return odcmClass.Properties.Where(prop => prop.IsNavigation());
        }

        public static bool IsNavigation(this OdcmProperty property)
        {

            bool isNavigationProperty = false;
            var classType = property.Type as OdcmClass;
            if (classType != null)
            {
                isNavigationProperty = classType.Kind == OdcmClassKind.Entity
                                     || classType.Kind == OdcmClassKind.MediaEntity;
            }
            return isNavigationProperty;
        }

        public static bool HasActions(this OdcmClass odcmClass)
        {
            return odcmClass.Methods.Any();
        }

        public static IEnumerable<OdcmMethod> Actions(this OdcmClass odcmClass)
        {
            return odcmClass.Methods;
        }

        public static bool IsAction(this OdcmMethod method)
        {
            return method.Verbs == OdcmAllowedVerbs.Post;
        }

        public static bool IsFunction(this OdcmMethod method)
        {
            return method.IsComposable; //TODO:REVIEW
        }

        public static bool IsStream(this OdcmProperty property)
        {
            return property.Type.Name.Contains("stream");
        }

        public static string GetNamespace(this OdcmModel model)
        {
            var @namespace = GetOdcmNamespace(model);
            return @namespace.Name;
        }

        public static OdcmClass AsOdcmClass(this OdcmObject odcmObject)
        {
            return odcmObject as OdcmClass;
        }

        public static OdcmEnum AsOdcmEnum(this OdcmObject odcmObject)
        {
            return odcmObject as OdcmEnum;
        }

        public static OdcmProperty AsOdcmProperty(this OdcmObject odcmObject)
        {
            return odcmObject as OdcmProperty;
        }

        public static OdcmMethod AsOdcmMethod(this OdcmObject odcmObject)
        {
            return odcmObject as OdcmMethod;
        }

        public static string NamespaceName(this OdcmModel model)
        {
            if (string.IsNullOrEmpty(ConfigurationService.Settings.NamespaceOverride))
            {
                var @namespace = GetOdcmNamespace(model).Name;
                var name = string.Format("{0}.{1}", ConfigurationService.Settings.NamespacePrefix, @namespace);
                return name.ToLower();
            }
            return ConfigurationService.Settings.NamespaceOverride;
        }

        public static string ODataPackageNamespace(this OdcmModel model)
        {
            var @namespace = NamespaceName(model);
            var package = string.Format("{0}.{1}", @namespace, "fetchers");
            return package.ToLower();
        }

        public static string GetEntityContainer(this OdcmModel model)
        {
            return model.EntityContainer.Name;
        }

        public static bool LongDescriptionContains(this OdcmObject odcmObject, string descriptionValue)
        {
            var descriptionParts = odcmObject.GetLongDescriptionSegments();
            return descriptionParts != null && descriptionParts.Contains(descriptionValue);
        }

        public static bool LongDescriptionStartsWith(this OdcmObject odcmObject, string descriptionValue)
        {
            var descriptionParts = odcmObject.GetLongDescriptionSegments();
            return descriptionParts != null && descriptionParts.Any(value => value.StartsWith(descriptionValue));
        }
        public static string[] GetLongDescriptionSegments(this OdcmObject odcmObject)
        {
            if (odcmObject.LongDescription != null)
            {
                return odcmObject.LongDescription.Split(';');
            }

            return null;
        }

        public static bool HasSpecialCollection(this OdcmMethod method)
        {
            return method.LongDescriptionStartsWith("specialCollection");
        }

        public static IEnumerable<SpecialMethodParameter> SpecialMethodParameters(this OdcmMethod method)
        {
            var paramList = new List<SpecialMethodParameter>();
            if (method.LongDescription != null)
            {
                var matches = Regex.Match(method.LongDescription, @"specialCollection=(([a-zA-Z0-9.]*):([a-zA-Z0-9.]*),*)*");
                if (matches != null)
                {
                    var names = matches.Groups[2].Captures;
                    var types = matches.Groups[3].Captures;
                    for (int i = 0; i < names.Count; i++)
                    {
                        paramList.Add(new SpecialMethodParameter(names[i].Value, types[i].Value));
                    }
                }
            }
            return paramList;
        }

    }


    public class SpecialMethodParameter : Object
    {
        private string typeString;
        public string Name { get; private set; }

        public string TypeString { get; private set; }

        public string FullName { get; private set; }

        public SpecialMethodParameter(string fullName, string typeString)
        {
            var strippedName = fullName.Split('.')[1];
            this.Name = strippedName;
            this.FullName = fullName;
            this.TypeString = typeString;
        }

    }
}
