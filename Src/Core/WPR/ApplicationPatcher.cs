using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Xml.Serialization;
using Mono.Cecil.Rocks;

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;
using WPR.Common;

namespace WPR
{
    public class ApplicationPatcher
    {
        // 7: public members whose type XmlSerializer cannot construct are marked
        //    [XmlIgnore], so a serializer for the containing type can be built.
        // 8: System.Data.Linq binds to the local-database implementation.
        // 9: List<T>[] members are serialized through a jagged T[][] surrogate.
        public static int Version => 9;

        private AssemblyNameReference FNACompRef;
        private AssemblyNameReference FNARef;
        private AssemblyNameReference SystemRunTimeRef;
        private AssemblyNameReference SystemThreadingRef;

        private AssemblyNameReference WindowsCompRef;
        private AssemblyNameReference MicrosoftPhoneRef;

        private AssemblyNameReference StandardCompRef;
        private AssemblyNameReference ServiceModelPrimitivesRef;
        private AssemblyNameReference ServiceModelHTTPRef;
        //private AssemblyNameReference SystemSecurityCryptographyRef; //!
        //private AssemblyNameReference SystemWindowsMediaImagingRef; //!

        private class TypePatchInfo
        {
            public String? NewName;
            public String? NewNamespace;
            public AssemblyNameReference? Reference;
        }

        private Dictionary<string, TypePatchInfo> Patches;
        private Dictionary<string, Type> MemberPatches;

        public ApplicationPatcher()
        {
            FNARef = AssemblyNameReference.Parse("FNA");
            FNACompRef = AssemblyNameReference.Parse("WPR.XnaCompability");
            SystemRunTimeRef = AssemblyNameReference.Parse("System.Runtime");
            SystemThreadingRef = AssemblyNameReference.Parse("System.Threading");
            WindowsCompRef = AssemblyNameReference.Parse("WPR.WindowsCompability");
            MicrosoftPhoneRef = AssemblyNameReference.Parse("Microsoft.Phone");

            ServiceModelPrimitivesRef = AssemblyNameReference.Parse("System.ServiceModel.Primitives");
            ServiceModelHTTPRef = AssemblyNameReference.Parse("System.ServiceModel.Http");

            StandardCompRef = AssemblyNameReference.Parse("WPR.StandardCompability");

            //SystemSecurityCryptographyRef = AssemblyNameReference.Parse("WPR.WindowsCompability");
            //SystemWindowsMediaImagingRef =  AssemblyNameReference.Parse("WPR.WindowsCompability");

            // *** Patches ***
            Patches = new Dictionary<string, TypePatchInfo>()
            {
                { "System.Diagnostics.Stopwatch", new TypePatchInfo()
                {
                    Reference = SystemRunTimeRef
                }
                },
                { "System.Threading.Mutex", new TypePatchInfo()
                {
                    Reference = SystemThreadingRef
                }
                },
                { "Microsoft.Xna.Framework.GraphicsDeviceManager", new TypePatchInfo()
                {
                    NewName = "GraphicsDeviceManager2",
                    NewNamespace = "WPR.XnaCompability",
                    Reference = FNACompRef
                }
                },
                { "Microsoft.Xna.Framework.Graphics.UIElementRenderer", new TypePatchInfo()
                {
                    NewNamespace = "WPR.XnaCompability.Graphics",
                    Reference = FNACompRef
                }
                },
                { "Microsoft.Xna.Framework.GameTimer", new TypePatchInfo()
                {
                    NewNamespace = "WPR.XnaCompability",
                    Reference = FNACompRef
                }
                },
                { "Microsoft.Xna.Framework.GameTimerEventArgs", new TypePatchInfo()
                {
                    NewNamespace = "WPR.XnaCompability",
                    Reference = FNACompRef
                }
                },
                { "System.Windows.Application", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.ApplicationUnhandledExceptionEventArgs", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.Deployment", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.Size", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.DependencyObject", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.UIElement", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.FrameworkElement", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.Visibility", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.Threading.Dispatcher", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Threading"
                }
                },
                { "System.Windows.Threading.DispatcherOperation", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Threading"
                }
                },
                { "System.Windows.Navigation.NavigationEventArgs", new TypePatchInfo()
                {
                    Reference = MicrosoftPhoneRef
                }
                },
                { "System.Windows.Navigation.NavigationFailedEventArgs", new TypePatchInfo()
                {
                    Reference = MicrosoftPhoneRef
                }
                },
                { "System.Windows.Navigation.NavigationFailedEventHandler", new TypePatchInfo()
                {
                    Reference = MicrosoftPhoneRef
                }
                },
                { "System.Windows.Threading.DispatcherTimer", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Threading"
                }
                },
                { "System.Windows.Interop.SilverlightHost", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Interop"
                }
                },
                { "System.Windows.Interop.Content", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Interop"
                }
                },
                { "System.Windows.Interop.Settings", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Interop"
                }
                },
                { "System.Windows.Resources.StreamResourceInfo", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Resources"
                }
                },
                { "System.IO.IsolatedStorage.IsolatedStorageSettings", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewName="IsolatedStorageSettings2", //RnD
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "Microsoft.Xna.Framework.Media.MediaSource", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.MediaSourceType", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.SongCollection", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.Artist", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.ArtistCollection", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.Album", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.AlbumCollection", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.Genre", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.MediaLibrary", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "System.IO.IsolatedStorage.IsolatedStorageFile", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewName = "IsolatedStorageFile2",
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.IO.IsolatedStorage.IsolatedStorageFileStream", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewName = "IsolatedStorageFileStream2",
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "Microsoft.Xna.Framework.Media.Playlist", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.PlaylistCollection", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "System.Windows.Media.SolidColorBrush", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Media"
                }
                },
                { "System.Windows.Media.Color", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Media"
                }
                },
                { "System.Windows.Thickness", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Media"
                }
                },
                { "System.Windows.ResourceDictionary", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.ServiceModel.XmlSerializerFormatAttribute", new TypePatchInfo()
                {
                    Reference = ServiceModelPrimitivesRef
                }
                },
                { "System.ServiceModel.BasicHttpBinding", new TypePatchInfo()
                {
                    Reference = ServiceModelHTTPRef
                }
                },
                { "System.ServiceModel.BasicHttpSecurity", new TypePatchInfo()
                {
                    Reference = ServiceModelHTTPRef
                }
                },
                { "System.ServiceModel.BasicHttpSecurityMode", new TypePatchInfo()
                {
                    Reference = ServiceModelHTTPRef
                }
                },
                { "System.Runtime.Serialization.Json.JavaScriptObjectDeserializer", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability.Serialization.Json"
                }
                },
                //!
                { "System.Security.Cryptography.ProtectedData", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    //RnD : if uncomment it, WPR.WindowsCompabilityProtectedData class will be used
                    NewName = "ProtectedData",
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                //!
                { "System.Windows.Media.Imaging.BitmapImage", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewName = "BitmapImage",//RnD
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                //!
                { "System.Windows.Media.Imaging.WriteableBitmap", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                 //!
                { "System.Windows.Media.Imaging.BitmapSource", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.MessageBox", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "Microsoft.Xna.Framework.Media.Picture", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "Microsoft.Xna.Framework.Media.PictureCollection", new TypePatchInfo()
                {
                    Reference = FNACompRef,
                    NewNamespace = "WPR.XnaCompability.Media"
                }
                },
                { "System.Windows.MessageBoxResult", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                { "System.Windows.MessageBoxButton", new TypePatchInfo()
                {
                    Reference = WindowsCompRef,
                    NewNamespace = "WPR.WindowsCompability"
                }
                },
                /* The phone's local-database types. Namespace and name are kept,
                 * so only the assembly a title binds to changes.
                 */
                { "System.Data.Linq.DataContext", new TypePatchInfo()
                {
                    Reference = StandardCompRef
                }
                },
                { "System.Data.Linq.Table`1", new TypePatchInfo()
                {
                    Reference = StandardCompRef
                }
                },
                { "System.Data.Linq.Mapping.TableAttribute", new TypePatchInfo()
                {
                    Reference = StandardCompRef
                }
                },
                { "System.Data.Linq.Mapping.ColumnAttribute", new TypePatchInfo()
                {
                    Reference = StandardCompRef
                }
                }
            };

            // *** Member Patches ***
            MemberPatches = new Dictionary<string, Type>
            {
                //TODO
                //{
                //    "System.Byte[] System.......::MethodName(System.Byte[],System.Byte[])",
                //    typeof(WPR.WindowsCompability.WebServices)
                //},

                // RnD ***************************************
                //{
                //    "Microsoft.Xna.Framework.GamerServices.LeaderboardReader Microsoft.Xna.Framework.GamerServices.LeaderboardReader::Read(Microsoft.Xna.Framework.GamerServices.LeaderboardIdentity, Microsoft.Xna.Framework.GamerServices.Gamer, Int32)",
                //    typeof(Microsoft.Xna.Framework.GamerServices2.LeaderboardReader)
                //},
                {
                    "System.String Microsoft.Phone.Info.DeviceStatus::get_DeviceName()",
                    typeof(WPR.WindowsCompability.DeviceStatus)
                },
                {
                    "System.String Microsoft.Phone.Info.DeviceStatus::get_DeviceManufacturer()",
                    typeof(WPR.WindowsCompability.DeviceStatus)
                },
                // *******************************************
                {
                    "System.Boolean System.IO.IsolatedStorage.IsolatedStorageSettings::TryGetValue(System.String, ByRef)",
                    typeof(WPR.WindowsCompability.IsolatedStorageSettings2)
                },
                {
                    "System.IO.IsolatedStorage.IsolatedStorageSettings System.IO.IsolatedStorage.IsolatedStorageSettings::get_ApplicationSettings()",
                    typeof(WPR.WindowsCompability.IsolatedStorageSettings2)
                },

                {
                    "System.Byte[] System.Security.Cryptography.ProtectedData::Protect(System.Byte[],System.Byte[])",
                    typeof(WPR.WindowsCompability.ProtectedData)
                },

                {
                    "System.Byte[] System.Security.Cryptography.ProtectedData::Unprotect(System.Byte[],System.Byte[])",
                    typeof(WPR.WindowsCompability.ProtectedData)
                },
                 
                //{
                //    "System.Windows.Media.Imaging.WriteableBitmap System.Windows.Media.Imaging.WriteableBitmap(System.Integer,System.Integer)",
                //    typeof(WPR.WindowsCompability.WriteableBitmap)
                //},
                //{
                //    "System.Void System.Windows.Media.Imaging.BitmapSource::SetSource()",
                //    typeof(WPR.WindowsCompability.BitmapSource)
                //},

                {
                    "System.Type System.Type::GetType(System.String,System.Boolean)",
                    typeof(WPR.WindowsCompability.Type2)
                },
                {
                    "Microsoft.Xna.Framework.Graphics.DisplayMode Microsoft.Xna.Framework.Graphics.GraphicsDevice::get_DisplayMode()",
                    typeof(WPR.XnaCompability.Graphics.GraphicsDevice2)
                },
                {
                    "Microsoft.Xna.Framework.Graphics.DisplayMode Microsoft.Xna.Framework.Graphics.GraphicsAdapter::get_CurrentDisplayMode()",
                    typeof(WPR.XnaCompability.Graphics.GraphicsAdapter2)
                },

                {
                    "System.String System.IO.Path::GetDirectoryName(System.String)",
                    typeof(WPR.WindowsCompability.Path2)
                },
                {
                    "System.String System.IO.Path::GetFileName(System.String)",
                    typeof(WPR.WindowsCompability.Path2)
                },
                {
                    "System.String System.IO.Path::GetFileNameWithoutExtension(System.String)",
                    typeof(WPR.WindowsCompability.Path2)
                },
                {
                    "System.Void System.GC::Collect()",
                    typeof(WPR.WindowsCompability.GC2)
                },

                {
                    "System.Xml.Linq.XElement System.Xml.Linq.XElement::Load(System.String)",
                    typeof(WPR.StandardCompability.Xml.Linq.XElement2)
                },

            };

        }//ApplicationPatcher

        /* XmlSerializer cannot map an array whose element type is a generic
         * collection: a List<T>[] member makes it throw a bare
         * NullReferenceException out of its own code generator, carrying no frame
         * of its own, so the fault reads as belonging to whichever title method
         * called Serialize. Trivial Pursuit's SaveGameState holds its collected
         * wedge colours that way, and creating the first save state is what
         * stopped the title entering a game.
         *
         * The member is hidden from the serializer and a jagged T[][] exposed in
         * its place, which maps correctly. Ignoring it outright would also let
         * the serializer build, and would quietly drop the wedges from every
         * saved game - a conversion costs a property and keeps the data.
         */
        private int PatchListArrayXmlMembers(ModuleDefinition module)
        {
            MethodReference? xmlIgnoreCtor = null;
            MethodDefinition? toJagged = null;
            MethodDefinition? fromJagged = null;
            int converted = 0;

            foreach (TypeDefinition type in module.GetTypes().ToList())
            {
                foreach (PropertyDefinition property in type.Properties.ToList())
                {
                    if (property.GetMethod == null || !property.GetMethod.IsPublic ||
                        property.SetMethod == null || !property.SetMethod.IsPublic ||
                        property.GetMethod.IsStatic)
                    {
                        continue;
                    }

                    TypeReference? element = ListArrayElement(property.PropertyType);
                    if (element == null)
                    {
                        continue;
                    }

                    if (property.CustomAttributes.Any(attribute =>
                        attribute.AttributeType.FullName == typeof(XmlIgnoreAttribute).FullName))
                    {
                        continue;
                    }

                    if (toJagged == null)
                    {
                        Type surrogate = typeof(WPR.StandardCompability.Serialization.XmlCollectionSurrogate);
                        AssemblyDefinition surrogateAssembly = AssemblyDefinition.ReadAssembly(
                            surrogate.Assembly.Location, new ReaderParameters { InMemory = true });
                        TypeDefinition surrogateType = surrogateAssembly.MainModule.GetType(surrogate.FullName);
                        toJagged = surrogateType.Methods.First(method => method.Name == "ToJagged");
                        fromJagged = surrogateType.Methods.First(method => method.Name == "FromJagged");
                    }

                    var jaggedType = new ArrayType(new ArrayType(module.ImportReference(element)));

                    var getter = new MethodDefinition(
                        $"get_{property.Name}XmlSurrogate",
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                        jaggedType);
                    ILProcessor getterIL = getter.Body.GetILProcessor();
                    getterIL.Emit(OpCodes.Ldarg_0);
                    getterIL.Emit(OpCodes.Call, property.GetMethod);
                    getterIL.Emit(OpCodes.Call, Instantiate(module, toJagged!, element));
                    getterIL.Emit(OpCodes.Ret);

                    var setter = new MethodDefinition(
                        $"set_{property.Name}XmlSurrogate",
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                        module.TypeSystem.Void)
                    {
                        Parameters = { new ParameterDefinition(jaggedType) }
                    };
                    ILProcessor setterIL = setter.Body.GetILProcessor();
                    setterIL.Emit(OpCodes.Ldarg_0);
                    setterIL.Emit(OpCodes.Ldarg_1);
                    setterIL.Emit(OpCodes.Call, Instantiate(module, fromJagged!, element));
                    setterIL.Emit(OpCodes.Call, property.SetMethod);
                    setterIL.Emit(OpCodes.Ret);

                    type.Methods.Add(getter);
                    type.Methods.Add(setter);
                    type.Properties.Add(new PropertyDefinition(
                        $"{property.Name}XmlSurrogate", PropertyAttributes.None, jaggedType)
                    {
                        GetMethod = getter,
                        SetMethod = setter
                    });

                    xmlIgnoreCtor ??= module.ImportReference(
                        typeof(XmlIgnoreAttribute).GetConstructor(Type.EmptyTypes));
                    property.CustomAttributes.Add(new CustomAttribute(xmlIgnoreCtor));
                    converted++;
                }
            }

            return converted;
        }

        private static MethodReference Instantiate(
            ModuleDefinition module, MethodDefinition method, TypeReference argument)
        {
            var instance = new GenericInstanceMethod(module.ImportReference(method));
            instance.GenericArguments.Add(module.ImportReference(argument));
            return instance;
        }

        /* T of a List<T>[] member, or null if the type is not that shape. */
        private static TypeReference? ListArrayElement(TypeReference type)
        {
            if (type is not ArrayType array || array.Rank != 1)
            {
                return null;
            }

            if (array.ElementType is not GenericInstanceType generic ||
                generic.GenericArguments.Count != 1)
            {
                return null;
            }

            string name = generic.ElementType.FullName;
            if (name != "System.Collections.Generic.List`1")
            {
                return null;
            }

            return generic.GenericArguments[0];
        }

        /* XmlSerializer builds its whole type map up front and refuses any public
         * member whose type it cannot construct - even a member that never appears
         * in a document. The phone's serializer validated lazily, so an engine
         * that hangs an XNA graphics object off a serializable content type worked
         * there and throws here. FlatRedBall's SpriteGridSave exposes a public
         * Texture2D field, marked with its own [ExternalInstance] rather than
         * [XmlIgnore], and Fusion Sentient constructs a serializer for the
         * containing scene type before it can draw anything. No .scnx in that
         * package contains the element.
         *
         * Marking such a member [XmlIgnore] cannot lose anything that worked. A
         * type with no parameterless constructor could never have been read back
         * whatever the platform, and without this no serializer for the containing
         * type can be built at all, which fails the whole title rather than one
         * member.
         */
        private static int PatchUnconstructableXmlMembers(ModuleDefinition module)
        {
            MethodReference? xmlIgnoreCtor = null;
            int marked = 0;

            bool AlreadyIgnored(ICustomAttributeProvider member) =>
                member.HasCustomAttributes && member.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName == typeof(XmlIgnoreAttribute).FullName);

            void MarkIgnored(ICustomAttributeProvider member)
            {
                xmlIgnoreCtor ??= module.ImportReference(
                    typeof(XmlIgnoreAttribute).GetConstructor(Type.EmptyTypes));
                member.CustomAttributes.Add(new CustomAttribute(xmlIgnoreCtor));
                marked++;
            }

            foreach (TypeDefinition type in module.GetTypes())
            {
                foreach (FieldDefinition field in type.Fields)
                {
                    if (!field.IsPublic || field.IsStatic || AlreadyIgnored(field))
                    {
                        continue;
                    }

                    if (IsUnconstructableForXml(field.FieldType))
                    {
                        MarkIgnored(field);
                    }
                }

                foreach (PropertyDefinition property in type.Properties)
                {
                    if (property.GetMethod == null || !property.GetMethod.IsPublic ||
                        property.GetMethod.IsStatic || AlreadyIgnored(property))
                    {
                        continue;
                    }

                    if (IsUnconstructableForXml(property.PropertyType))
                    {
                        MarkIgnored(property);
                    }
                }
            }

            return marked;
        }

        /* XmlSerializer looks through arrays and generic collections to the thing
         * it would have to build, so this does the same before asking whether that
         * type has the parameterless constructor it requires. Anything that cannot
         * be resolved is left alone: not patching leaves the title exactly as it
         * was, while guessing could suppress a member that serialized fine.
         */
        private static bool IsUnconstructableForXml(TypeReference type)
        {
            while (type is ArrayType array)
            {
                type = array.ElementType;
            }

            if (type is GenericInstanceType generic)
            {
                if (generic.GenericArguments.Count != 1)
                {
                    return false;
                }

                return IsUnconstructableForXml(generic.GenericArguments[0]);
            }

            if (type.IsPrimitive || type.IsGenericParameter)
            {
                return false;
            }

            TypeDefinition? resolved;
            try
            {
                resolved = type.Resolve();
            }
            catch
            {
                return false;
            }

            if (resolved == null || resolved.IsValueType || resolved.IsInterface || !resolved.IsClass)
            {
                return false;
            }

            if (resolved.FullName == "System.Object" || resolved.FullName == "System.String")
            {
                return false;
            }

            return !resolved.Methods.Any(method =>
                method.IsConstructor && method.IsPublic && !method.IsStatic &&
                method.Parameters.Count == 0);
        }

        private void PatchRelaxedXmlNullableAttribTextSerialize(ModuleDefinition? module)
        {
            Queue<TypeDefinition> typeScanQueue = new Queue<TypeDefinition>();
            foreach (var typeDef in module!.Types)
            {
                typeScanQueue.Enqueue(typeDef);
            }

            CustomAttribute? xmlIgnoreAttrib = null;

            // Patch type for resolve XML library incompability
            while (typeScanQueue.Count != 0)
            {
                TypeDefinition type = typeScanQueue.Dequeue();

                if (type.HasNestedTypes)
                {
                    foreach (var typeNested in type.NestedTypes)
                    {
                        typeScanQueue.Enqueue(typeNested);
                    }
                }

                foreach (var field in type.Fields)
                {
                    CustomAttribute? xmlNonNullableProp = null;

                    foreach (var attrib in field.CustomAttributes)
                    {
                        if (attrib.AttributeType.FullName == typeof(XmlAttributeAttribute).FullName)
                        {
                            xmlNonNullableProp = attrib;
                            break;
                        }
                    }

                    if (xmlNonNullableProp == null)
                    {
                        continue;
                    }

                    if (field.FieldType.FullName.Contains("System.Nullable"))
                    {
                        var actualFieldType = (field.FieldType as GenericInstanceType)!.GenericArguments[0];

                        // Generate holder getter/setter
                        var getterMethod = new MethodDefinition($"get_{field.Name}SerializableHolder",
                            MethodAttributes.Public, actualFieldType);

                        var getterGen = getterMethod.Body.GetILProcessor();

                        var nullableRefTypeGeneric = module.ImportReference(
                            Type.GetType("System.Nullable`1")!);

                        var nullableRefType =
                            nullableRefTypeGeneric.MakeGenericInstanceType(new TypeReference[]
                            { actualFieldType });

                        // Emit getter
                        getterGen.Emit(OpCodes.Ldarg_0);
                        getterGen.Emit(OpCodes.Ldflda, field);
                        getterGen.Emit(OpCodes.Call, new MethodReference("get_Value",
                            nullableRefTypeGeneric.GenericParameters[0])
                        {
                            HasThis = true,
                            DeclaringType = nullableRefType
                        });

                        getterGen.Emit(OpCodes.Ret);

                        // Emit setter
                        var setterMethod = new MethodDefinition($"set_{field.Name}SerializableHolder",
                            MethodAttributes.Public, module.TypeSystem.Void)
                        {
                            Parameters = { new ParameterDefinition(actualFieldType) },
                            HasThis = true
                        };
                        var setterGen = setterMethod.Body.GetILProcessor();

                        setterGen.Emit(OpCodes.Ldarg_0);
                        setterGen.Emit(OpCodes.Ldarg_1);
                        setterGen.Emit(OpCodes.Newobj, new MethodReference(".ctor",
                            module.TypeSystem.Void, nullableRefType)
                        {
                            Parameters = { new ParameterDefinition(
                                nullableRefTypeGeneric.GenericParameters[0]) },
                            HasThis = true
                        });

                        setterGen.Emit(OpCodes.Stfld, field);
                        setterGen.Emit(OpCodes.Ret);

                        // Emit skip serialize consideration
                        var shouldSerializeMethod = new MethodDefinition(
                            $"ShouldSerialize{field.Name}SerializableHolder",
                            MethodAttributes.Public, module.TypeSystem.Boolean);

                        var shouldSerializeGen = shouldSerializeMethod.Body.GetILProcessor();

                        shouldSerializeGen.Emit(OpCodes.Ldarg_0);
                        shouldSerializeGen.Emit(OpCodes.Ldflda, field);
                        shouldSerializeGen.Emit(OpCodes.Call, new MethodReference(
                            "HasValue", module.TypeSystem.Boolean, nullableRefType)
                        {
                            HasThis = true
                        });
                        shouldSerializeGen.Emit(OpCodes.Ret);

                        type.Methods.Add(shouldSerializeMethod);
                        type.Methods.Add(getterMethod);
                        type.Methods.Add(setterMethod);

                        var propSeri = new PropertyDefinition(
                            $"{field.Name}SerializableHolder", PropertyAttributes.None, actualFieldType)
                        {
                            GetMethod = getterMethod,
                            SetMethod = setterMethod
                        };

                        type.Properties.Add(propSeri);

                        if (xmlIgnoreAttrib == null)
                        {
                            xmlIgnoreAttrib = new CustomAttribute(module.ImportReference(typeof(XmlIgnoreAttribute).
                                GetConstructor(Type.EmptyTypes)));
                        }

                        field.CustomAttributes.Remove(xmlNonNullableProp);
                        field.CustomAttributes.Add(xmlIgnoreAttrib);

                        // Add attribute if they already gave name, else we need to be creative
                        if (xmlNonNullableProp.HasConstructorArguments)
                        {
                            propSeri.CustomAttributes.Add(xmlNonNullableProp);
                        }
                        else
                        {
                            var attributeType = (xmlNonNullableProp.AttributeType.FullName
                                == typeof(XmlAttributeAttribute).FullName)
                                    ? typeof(XmlAttributeAttribute)
                                    : typeof(XmlTextAttribute);

                            MethodReference methodConstructor = module.ImportReference(attributeType
                                .GetConstructor(new Type[] { typeof(String) }));

                            propSeri.CustomAttributes.Add(new CustomAttribute(methodConstructor)
                            {
                                ConstructorArguments = {
                                    new CustomAttributeArgument(module.TypeSystem.String, field.Name) }
                            });
                        }
                    }
                }
            }
        }

        // PatchDll(string modulePath)
        public void PatchDll(string modulePath)
        {
            using InMemoryAssemblyResolver resolver = CreateResolver(Path.GetDirectoryName(modulePath)!);
            PatchDll(modulePath, resolver);
        }

        private void PatchDll(string modulePath, IAssemblyResolver resolver)
        {
            string pristineModulePath = modulePath + ".original";
            bool hadPristineModule = File.Exists(pristineModulePath);
            string sourceModulePath = hadPristineModule
                ? pristineModulePath
                : modulePath;

            // ReadAssembly
            AssemblyDefinition assemblyData =
                Mono.Cecil.AssemblyDefinition.ReadAssembly(sourceModulePath, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    InMemory = true,
                    ReadSymbols = false
                });

            Mono.Cecil.ModuleDefinition module = assemblyData.MainModule;

            FoldMetadataTokenReads(module);
            RedirectReflectiveFieldReads(module);
            RedirectAssemblyTypeLookups(module);

            assemblyData.Name.Name = AssemblyNameStandardization.Process(assemblyData.Name.Name);

            string modulePathNameStandardized = Path.Combine(
                Path.GetDirectoryName(modulePath)!,
               AssemblyNameStandardization.Process(
                    Path.GetFileNameWithoutExtension(modulePath)) +
                Path.GetExtension(modulePath));

            AssemblyNameReference? xnaGameServices = null;

            PatchNeutralResourcesLanguage(module);

            // Remove unneeded attribute (pretty sure!)
            foreach (var attrib in module.Assembly.CustomAttributes)
            {
                if (attrib.AttributeType.FullName ==
                    "System.Runtime.CompilerServices.CodeGenerationAttribute")
                {
                    module.Assembly.CustomAttributes.Remove(attrib);
                    break;
                }
            }

            // module.AssemblyReferences cycle 
            foreach (var refer in module.AssemblyReferences)
            {
                if (refer.Name.Contains("Microsoft.Xna"))
                {
                    if (refer.Name.Contains("GamerServicesExtensions"))
                    {
                        refer.Name = "Microsoft.Xna.Framework.GamerServices";
                        xnaGameServices = refer;
                    }
                    else if (refer.Name.Contains("GamerServices"))
                    {
                        xnaGameServices = refer;
                    }
                    else
                    {
                        refer.Name = FNARef.Name;
                        refer.Version = FNARef.Version;
                        refer.PublicKey = FNARef.PublicKey;
                    }
                }
                else if (refer.Name.Equals("mscorlib.Extensions",
                    StringComparison.OrdinalIgnoreCase))
                {
                    refer.Name = SystemRunTimeRef.Name;
                    refer.Version = SystemRunTimeRef.Version;
                    refer.PublicKey = SystemRunTimeRef.PublicKey;
                }
                else if (refer.Name.Equals("System.ServiceModel",
                    StringComparison.OrdinalIgnoreCase))
                {
                    refer.Name = ServiceModelPrimitivesRef.Name;
                    refer.Version = ServiceModelPrimitivesRef.Version;
                    refer.PublicKey = ServiceModelPrimitivesRef.PublicKey;
                }
            }

            xnaGameServices ??= module.AssemblyReferences.FirstOrDefault(reference =>
                reference.Name.Equals("Microsoft.Xna.Framework.GamerServices", StringComparison.OrdinalIgnoreCase));
            if (xnaGameServices == null)
            {
                xnaGameServices = AssemblyNameReference.Parse("Microsoft.Xna.Framework.GamerServices");
                module.AssemblyReferences.Add(xnaGameServices);
            }

            //RnD
            PatchRelaxedXmlNullableAttribTextSerialize(module);
            PatchListArrayXmlMembers(module);
            PatchUnconstructableXmlMembers(module);

            // Add AssemblyReferences
            module.AssemblyReferences.Add(FNACompRef);
            module.AssemblyReferences.Add(WindowsCompRef);
            module.AssemblyReferences.Add(MicrosoftPhoneRef);
            module.AssemblyReferences.Add(SystemRunTimeRef);
            module.AssemblyReferences.Add(SystemThreadingRef);
            module.AssemblyReferences.Add(ServiceModelPrimitivesRef);
            module.AssemblyReferences.Add(ServiceModelHTTPRef);
            module.AssemblyReferences.Add(StandardCompRef);
            //module.AssemblyReferences.Add(SystemSecurityCryptographyRef);//!
            //module.AssemblyReferences.Add(SystemWindowsMediaImagingRef);//

            // create Ref. Patch Cache
            Dictionary<string, TypeReference> typeRefPatchCache
                = new Dictionary<string, TypeReference>();

            // module.GetMemberReferences cycle
            foreach (var memberRef in module.GetMemberReferences())
            {
                //if (memberRef.FullName.Contains("Collect"))
                //{
                //    Debug.WriteLine("[Collect] memberRef fullname: "
                //        + memberRef.FullName);
                //}

                foreach (var patch in MemberPatches)
                {
                    /*
                    if (memberRef.FullName.Contains("Collect"))
                    {
                        //Debug.WriteLine("[TeSTING] memberRef.FullName.Contains : Collect");
                        Debug.WriteLine("[TeSTING] memberRef.FullName Contains Collect: " 
                            + memberRef.FullName);
                    }
                    */

                    if (memberRef.FullName == patch.Key)
                    {
                        if (typeRefPatchCache.ContainsKey(patch.Value.FullName!))
                        {
                            memberRef.DeclaringType = typeRefPatchCache[patch.Value.FullName!];
                        }
                        else
                        {
                            memberRef.DeclaringType = module.ImportReference(patch.Value);
                            typeRefPatchCache.Add(patch.Value.FullName!, memberRef.DeclaringType);
                        }
                    }
                }
            }

            // cycle existing refs...
            foreach (var existingRef in module.GetTypeReferences())
            {
                existingRef.Name = AssemblyNameStandardization.Process(existingRef.Name);

                if (existingRef.Namespace
                    == "Microsoft.Xna.Framework.GamerServices")
                {
                    existingRef.Scope = xnaGameServices;
                }
                else if (existingRef.FullName
                    == "Microsoft.Xna.Framework.GamerServicesExtensions.GamerServicesComponent")
                {
                    existingRef.Scope = xnaGameServices;
                }
                else
                {
                    if (Patches.ContainsKey(existingRef.FullName))
                    {
                        TypePatchInfo patch = Patches[existingRef.FullName];
                        if (patch != null)
                        {
                            if (patch.NewName != null)
                            {
                                existingRef.Name = patch.NewName;
                            }

                            if (patch.NewNamespace != null)
                            {
                                existingRef.Namespace = patch.NewNamespace;
                            }

                            if (patch.Reference != null)
                            {
                                existingRef.Scope = patch.Reference;
                            }
                        }
                    }
                }
            }//for...


            string patchedModulePath = modulePath + ".new";

            // A write failure must abort the install; otherwise an unpatched DLL
            // can be recorded as successfully installed.
            try
            {
                assemblyData.Write(patchedModulePath);
            }
            catch
            {
                if (File.Exists(patchedModulePath))
                {
                    File.Delete(patchedModulePath);
                }

                throw;
            }
            finally
            {
                assemblyData.Dispose();
            }

            if (!Path.GetFullPath(modulePathNameStandardized).Equals(
                    Path.GetFullPath(modulePath),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                File.Exists(modulePathNameStandardized))
            {
                File.Delete(patchedModulePath);
                throw new IOException($"Patching '{modulePath}' would overwrite '{modulePathNameStandardized}'.");
            }

            // Preserve the package assembly once. Version upgrades must always be
            // regenerated from it rather than compounding prior Cecil rewrites.
            if (!hadPristineModule)
            {
                File.Move(modulePath, modulePathNameStandardized + ".original", true);
            }

            // .dll.new - > .dll
            try
            {
                File.Move(patchedModulePath, modulePathNameStandardized, true);
            }
            catch
            {
                if (!hadPristineModule && File.Exists(modulePathNameStandardized + ".original"))
                {
                    File.Move(modulePathNameStandardized + ".original", modulePath, true);
                }

                if (File.Exists(patchedModulePath))
                {
                    File.Delete(patchedModulePath);
                }

                throw;
            }
        }//PatchDll

        // Route reflective field reads through the shim that reports a missing
        // target the way the phone does. A game's callvirt already has the
        // FieldInfo below its argument, which is exactly the static shim's
        // parameter order, so only the opcode and the operand change.
        internal static int RedirectReflectiveFieldReads(ModuleDefinition module)
        {
            const string fieldGetValue =
                "System.Object System.Reflection.FieldInfo::GetValue(System.Object)";

            MethodReference? shim = null;
            int redirected = 0;

            foreach (MethodDefinition method in module.GetTypes().SelectMany(type => type.Methods))
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Call)
                    {
                        continue;
                    }

                    if (instruction.Operand is not MethodReference called ||
                        called.FullName != fieldGetValue)
                    {
                        continue;
                    }

                    shim ??= module.ImportReference(
                        typeof(WPR.WindowsCompability.Reflection2).GetMethod(
                            nameof(WPR.WindowsCompability.Reflection2.GetFieldValue),
                            new[] { typeof(System.Reflection.FieldInfo), typeof(object) }));

                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = shim;
                    redirected++;
                }
            }

            return redirected;
        }

        // Route assembly-scoped type lookups through the shim that also consults
        // the core library, the way the phone's reflection resolved an
        // unqualified BCL name. Both calls are an Assembly receiver and a single
        // string, which is already the static shim's parameter order, so as with
        // the field reads only the opcode and the operand change.
        internal static int RedirectAssemblyTypeLookups(ModuleDefinition module)
        {
            const string assemblyGetType =
                "System.Type System.Reflection.Assembly::GetType(System.String)";
            const string assemblyCreateInstance =
                "System.Object System.Reflection.Assembly::CreateInstance(System.String)";

            MethodReference? typeShim = null;
            MethodReference? instanceShim = null;
            int redirected = 0;

            foreach (MethodDefinition method in module.GetTypes().SelectMany(type => type.Methods))
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Call)
                    {
                        continue;
                    }

                    if (instruction.Operand is not MethodReference called)
                    {
                        continue;
                    }

                    if (called.FullName == assemblyGetType)
                    {
                        typeShim ??= module.ImportReference(
                            typeof(WPR.WindowsCompability.Reflection2).GetMethod(
                                nameof(WPR.WindowsCompability.Reflection2.GetAssemblyType),
                                new[] { typeof(System.Reflection.Assembly), typeof(string) }));

                        instruction.OpCode = OpCodes.Call;
                        instruction.Operand = typeShim;
                        redirected++;
                    }
                    else if (called.FullName == assemblyCreateInstance)
                    {
                        instanceShim ??= module.ImportReference(
                            typeof(WPR.WindowsCompability.Reflection2).GetMethod(
                                nameof(WPR.WindowsCompability.Reflection2.CreateAssemblyInstance),
                                new[] { typeof(System.Reflection.Assembly), typeof(string) }));

                        instruction.OpCode = OpCodes.Call;
                        instruction.Operand = instanceShim;
                        redirected++;
                    }
                }
            }

            return redirected;
        }

        internal static int FoldMetadataTokenReads(ModuleDefinition module)
        {
            const string metadataTokenGetter =
                "System.Int32 System.Reflection.MemberInfo::get_MetadataToken()";

            HashSet<MethodDefinition> tokenAccessors = module.GetTypes()
                .SelectMany(type => type.Methods)
                .Where(method =>
                    method.IsStatic &&
                    method.ReturnType.FullName == module.TypeSystem.Int32.FullName &&
                    method.Parameters.Count == 1 &&
                    method.Parameters[0].ParameterType.FullName == "System.Type" &&
                    method.HasBody &&
                    method.Body.Instructions.Count(instruction =>
                        instruction.Operand is MethodReference called &&
                        called.FullName == metadataTokenGetter) == 1 &&
                    method.Body.Instructions
                        .Where(instruction => instruction.Operand is MethodReference)
                        .All(instruction =>
                            ((MethodReference)instruction.Operand).FullName == metadataTokenGetter))
                .ToHashSet();

            if (tokenAccessors.Count == 0)
            {
                return 0;
            }

            int folded = 0;
            foreach (MethodDefinition method in module.GetTypes().SelectMany(type => type.Methods))
            {
                if (!method.HasBody)
                {
                    continue;
                }

                var instructions = method.Body.Instructions;
                for (int index = 2; index < instructions.Count; index++)
                {
                    Instruction tokenLoad = instructions[index - 2];
                    Instruction typeConversion = instructions[index - 1];
                    Instruction accessorCall = instructions[index];

                    if (tokenLoad.OpCode != OpCodes.Ldtoken ||
                        tokenLoad.Operand is not TypeDefinition type ||
                        type.Module != module ||
                        type.MetadataToken.TokenType != TokenType.TypeDef ||
                        type.MetadataToken.RID == 0 ||
                        typeConversion.Operand is not MethodReference conversion ||
                        conversion.FullName !=
                            "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)" ||
                        accessorCall.Operand is not MethodDefinition accessorDefinition ||
                        !tokenAccessors.Contains(accessorDefinition))
                    {
                        continue;
                    }

                    tokenLoad.OpCode = OpCodes.Nop;
                    tokenLoad.Operand = null;
                    typeConversion.OpCode = OpCodes.Nop;
                    typeConversion.Operand = null;
                    accessorCall.OpCode = OpCodes.Ldc_I4;
                    accessorCall.Operand = type.MetadataToken.ToInt32();
                    folded++;
                }
            }

            return folded;
        }

        private static void PatchNeutralResourcesLanguage(ModuleDefinition module)
        {
            foreach (CustomAttribute attribute in module.Assembly.CustomAttributes)
            {
                if (attribute.AttributeType.FullName !=
                    "System.Resources.NeutralResourcesLanguageAttribute" ||
                    attribute.ConstructorArguments.Count == 0 ||
                    attribute.ConstructorArguments[0].Value is not string cultureName)
                {
                    continue;
                }

                try
                {
                    _ = CultureInfo.GetCultureInfo(cultureName);
                    continue;
                }
                catch (CultureNotFoundException) when (
                    cultureName.Equals("English", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.ConstructorArguments[0] = new CustomAttributeArgument(
                        attribute.ConstructorArguments[0].Type, "en-US");
                }
            }
        }

        private static InMemoryAssemblyResolver CreateResolver(string appRootPath)
        {
            var resolver = new InMemoryAssemblyResolver();
            var searchDirectories = new HashSet<string>(
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            void AddSearchDirectory(string? directory)
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                string fullPath = Path.GetFullPath(directory);
                if (searchDirectories.Add(fullPath))
                {
                    resolver.AddSearchDirectory(fullPath);
                }
            }

            AddSearchDirectory(appRootPath);
            foreach (string dllPath in Directory.EnumerateFiles(appRootPath, "*.dll", SearchOption.AllDirectories))
            {
                AddSearchDirectory(Path.GetDirectoryName(dllPath));
            }
            AddSearchDirectory(Path.GetDirectoryName(typeof(ApplicationPatcher).Assembly.Location));
            AddSearchDirectory(AppContext.BaseDirectory);
            return resolver;
        }

        private sealed class InMemoryAssemblyResolver : DefaultAssemblyResolver
        {
            public override AssemblyDefinition Resolve(AssemblyNameReference name) =>
                Resolve(name, new ReaderParameters());

            public override AssemblyDefinition Resolve(
                AssemblyNameReference name, ReaderParameters parameters)
            {
                parameters.AssemblyResolver = this;
                parameters.InMemory = true;
                parameters.ReadSymbols = false;
                return base.Resolve(name, parameters);
            }
        }

        public void Patch(string appRootPath, Action<int> progress, CancellationToken token)
        {
            List<string> filenameList = Directory.EnumerateFiles(appRootPath,
                "*.dll", SearchOption.AllDirectories).ToList();
            int totalCount = filenameList.Count;
            int current = 0;

            if (totalCount == 0)
            {
                progress(100);
                return;
            }

            using InMemoryAssemblyResolver resolver = CreateResolver(appRootPath);
            foreach (var filename in filenameList)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    bool hasMetadata;
                    using (var stream = File.OpenRead(filename))
                    using (var peReader = new PEReader(stream))
                    {
                        hasMetadata = peReader.HasMetadata;
                    }

                    if (hasMetadata)
                    {
                        PatchDll(filename, resolver);
                        Debug.WriteLine($"[i] Patching DLL with path: {filename}.\n");
                    }
                    else
                    {
                        Debug.WriteLine($"[i] Preserving non-managed DLL with path: {filename}.\n");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(LogCategory.AppInstall, $"Fail to patch DLL with path: {filename}. Error:\n{ex}");
                    throw new InvalidDataException($"Failed to patch application DLL '{filename}'.", ex);
                }

                current++;
                progress((int)(current * 100.0 / totalCount));
            }
        }
    }
}
