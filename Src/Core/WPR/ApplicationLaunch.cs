using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using System.Runtime.Loader;
using WPR.Models;
using WPR.Common;

using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework.GamerServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WPR.XnaCompability;

namespace WPR
{
    public static class ApplicationLaunch
    {
        private static string CurrentProductFolder => Path.Combine(Configuration.Current.DataPath(Application.DataStoreFolder),
            WindowsCompability.Application.Current!.ProductId!);

        static ApplicationLaunch()
        {
            AssemblyLoadContext.Default.Resolving += ResolveAssembly;
        }

        private static Assembly? ResolveAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName.Name))
            {
                return null;
            }

            string? productFolder = null;
            try
            {
                if (Configuration.Current != null && WindowsCompability.Application.Current?.ProductId != null)
                {
                    productFolder = CurrentProductFolder;
                }
            }
            catch
            {
                // Ignore; fall back to host assemblies only.
            }

            if (productFolder != null)
            {
                var gamePath = Path.Combine(productFolder, assemblyName.Name + ".dll");
                if (File.Exists(gamePath))
                {
                    return loadContext.LoadFromAssemblyPath(gamePath);
                }
            }

            var alreadyLoaded = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded != null)
            {
                return alreadyLoaded;
            }

            foreach (var searchDir in GetHostAssemblySearchPaths())
            {
                var hostPath = Path.Combine(searchDir, assemblyName.Name + ".dll");
                if (File.Exists(hostPath))
                {
                    return loadContext.LoadFromAssemblyPath(hostPath);
                }
            }

            return null;
        }

        private static IEnumerable<string> GetHostAssemblySearchPaths()
        {
            var paths = new List<string>();

            if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
            {
                paths.Add(AppContext.BaseDirectory);
            }

            var hostAssemblyDir = Path.GetDirectoryName(typeof(ApplicationLaunch).Assembly.Location);
            if (!string.IsNullOrEmpty(hostAssemblyDir))
            {
                paths.Add(hostAssemblyDir);
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static async Task Start(Application app, Action<DisplayOrientation>? requestOrientation = null)
        {
            if (app.ApplicationType != ApplicationType.XNA)
            {
                throw new NotSupportedException("Only XNA app is supported!");
            }

            // Setting game folder path
            WindowsCompability.Application.Current.ProductId = app.ProductId;
            string folderPath = CurrentProductFolder;

            FNAPlatform.TitleLocation = folderPath;
            string curDir = Directory.GetCurrentDirectory();

            EnsureGameAssembliesPatched(folderPath, app);

            Assembly assem = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(folderPath, AssemblyNameStandardization.Process(app.Assembly)));

            Directory.SetCurrentDirectory(folderPath);

            // Instatiate
            Type? mainType = assem.GetType(app.EntryPoint);

            // Run on separate thread to not affect the UI
            await Task.Run(() =>
            {
                SignedInGamer.Reset();
                using (Game? obj = Activator.CreateInstance(mainType!) as Game)
                {
                    obj!.IsMouseVisible = true;
                    obj!.Window.Title = $"{app.Name} - {app.Author} (Publisher: {app.Publisher})";

#if !__MOBILE__
                    TouchPanel.MouseAsTouch = true;
#endif
                    /* Leave EnabledGestures at the XNA default of None and let the
                     * title opt in, exactly as it would on the phone. Enabling every
                     * gesture here produced samples that titles reading only
                     * TouchPanel.GetState never dequeue, so the gesture queue grew for
                     * the life of the process, and it forced DoubleTap on for titles
                     * that deliberately leave it off: the detector answers a second tap
                     * inside 300ms with a DoubleTap and suppresses the Tap, so quick
                     * repeated taps went missing.
                     */

                    GraphicsDeviceManager2.RequestOrientation = requestOrientation;
                    GamerServicesDispatcher.WindowHandle = obj.Window.Handle;

                    /* Launching must reach the title before Activated does. It used
                     * to hang off Activated, which put it behind the title's own
                     * Activated handler in the same multicast delegate, so a title
                     * that reads launch-created state from Activated threw, and the
                     * throw stopped the delegate before Launching was ever raised.
                     * Lode Runner never left a blank screen for exactly that reason.
                     */
                    obj.Starting += (obj, args) =>
                    {
                        PhoneApplicationService.Current!.HandleApplicationStart(true);
                    };

                    GraphicsDeviceManager? manager = obj.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager;
                    if (manager != null)
                    {
                        manager.PreparingDeviceSettings += (obj, args) =>
                        {
                            var presentation = args.GraphicsDeviceInformation.PresentationParameters;
#if !__MOBILE__
                            if (presentation.BackBufferFormat != SurfaceFormat.Color)
                            {
                                Log.Info(LogCategory.AppList,
                                    $"Normalizing phone backbuffer format {presentation.BackBufferFormat} to Color");
                                presentation.BackBufferFormat = SurfaceFormat.Color;
                            }

                            /* The phone had one screen, so a title asking for
                             * fullscreen was asking for 480x800, not for the
                             * desktop. Honouring it literally gives a window the
                             * width of every monitor attached - KenKen requests
                             * 480x800 fullscreen and was drawn across 3840x1080,
                             * which is also why a tap aimed at its prompt landed
                             * nowhere near it. A phone title runs windowed at the
                             * size it asked for, the way a desktop game defaults.
                             * GraphicsDeviceManager2 already refuses the flag when
                             * a title sets it through the manager; this catches the
                             * ones that set it on the presentation parameters, and
                             * runs last because the host subscribes after the title.
                             */
                            if (presentation.IsFullScreen)
                            {
                                Log.Info(LogCategory.AppList,
                                    "Normalizing phone fullscreen request to a windowed " +
                                    $"{presentation.BackBufferWidth}x{presentation.BackBufferHeight} surface");
                                presentation.IsFullScreen = false;
                            }
#endif
                            Log.Info(LogCategory.AppList,
                                $"Presentation parameters: {presentation.BackBufferWidth}x{presentation.BackBufferHeight}, " +
                                $"format={presentation.BackBufferFormat}, depth={presentation.DepthStencilFormat}, " +
                                $"fullscreen={presentation.IsFullScreen}, samples={presentation.MultiSampleCount}, " +
                                $"interval={presentation.PresentationInterval}, orientation={presentation.DisplayOrientation}, " +
                                $"window=0x{presentation.DeviceWindowHandle.ToInt64():X}");
                            GraphicsDeviceManager2.RequestOrientationChange(
                                presentation.BackBufferWidth,
                                presentation.BackBufferHeight
                            );
                        };
                    }

                    try
                    {
                        // Run the game and capture any exceptions to produce richer diagnostics.
                        try
                        {
                            obj.Run();
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                string diag = BuildDiagnostics(obj, ex);

                                // Log to the configured logger
                                Log.Error(LogCategory.AppList, $"Game threw during Run: {ex}");

                                // Also try to write diagnostics to a file next to the game's folder so it survives process exits
                                try
                                {
                                    string diagFile = Path.Combine(folderPath, "wpr_game_diagnostic.txt");
                                    File.WriteAllText(diagFile, diag);
                                }
                                catch (Exception fileEx)
                                {
                                    Log.Warn(LogCategory.AppList, $"Failed to write diagnostic file: {fileEx}");
                                }
                            }
                            catch (Exception logEx)
                            {
                                // Avoid swallowing the original exception but at least log that diagnostics failed
                                Log.Warn(LogCategory.AppList, $"Diagnostics failed: {logEx}");
                            }

                            // Rethrow so outer code still receives the original failure
                            throw;
                        }

                        try
                        {
                            PhoneApplicationService.Current!.HandleApplicationExit();
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(LogCategory.AppList, $"Ignored clean-up exception:\n {ex}");
                        }

                        obj.Exit();
                    }
                    finally
                    {
                        // Ensure current directory is restored to previous value to avoid surprising callers
                        try
                        {
                            Directory.SetCurrentDirectory(curDir);
                        }
                        catch { }
                    }
                }
            });
        }

        private static void EnsureGameAssembliesPatched(string folderPath, Application app)
        {
            if (app.PatchedVersion >= ApplicationPatcher.Version)
            {
                return;
            }

            Log.Info(LogCategory.AppList, $"Patching game assemblies in {folderPath}");
            var patcher = new ApplicationPatcher();
            patcher.Patch(folderPath, _ => { }, CancellationToken.None);
            app.PatchedVersion = ApplicationPatcher.Version;
            ApplicationContext.Current.SaveChanges();
        }

        private static string BuildDiagnostics(Game? obj, Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== WPR Game Diagnostic ===");
            sb.AppendLine("Timestamp: " + DateTime.UtcNow.ToString("o"));
            sb.AppendLine();

            sb.AppendLine("Exception:");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();

            if (obj == null)
            {
                sb.AppendLine("Game instance is null.");
                return sb.ToString();
            }

            try
            {
                Type t = obj.GetType();
                sb.AppendLine($"Game type: {t.FullName}");

                // Common Game properties
                try { sb.AppendLine($"Window: {(obj.Window == null ? "<null>" : obj.Window.GetType().FullName)}"); } catch { sb.AppendLine("Window: <error>"); }
                try { sb.AppendLine($"Window.Title: {(obj.Window?.Title ?? "<null>")}"); } catch { sb.AppendLine("Window.Title: <error>"); }
                try { sb.AppendLine($"GraphicsDevice: {(obj.GraphicsDevice == null ? "<null>" : obj.GraphicsDevice.GetType().FullName)}"); } catch { sb.AppendLine("GraphicsDevice: <error>"); }
                try { sb.AppendLine($"Content: {(obj.Content == null ? "<null>" : obj.Content.GetType().FullName)}"); } catch { sb.AppendLine("Content: <error>"); }
                try { sb.AppendLine($"Services: {(obj.Services == null ? "<null>" : obj.Services.GetType().FullName)}"); } catch { sb.AppendLine("Services: <error>"); }

                sb.AppendLine();

                // Inspect instance fields and properties (best-effort, do not throw)
                sb.AppendLine("Instance fields:");
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    try
                    {
                        object? val = field.GetValue(obj);
                        sb.AppendLine($"- {field.Name} ({field.FieldType.FullName}): {(val == null ? "<null>" : val.GetType().FullName)}");
                    }
                    catch (Exception fex)
                    {
                        sb.AppendLine($"- {field.Name}: <error reading> {fex.Message}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Instance properties:");
                foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!prop.CanRead) continue;
                    try
                    {
                        object? val = prop.GetValue(obj);
                        sb.AppendLine($"- {prop.Name} ({prop.PropertyType.FullName}): {(val == null ? "<null>" : val.GetType().FullName)}");
                    }
                    catch (Exception pex)
                    {
                        sb.AppendLine($"- {prop.Name}: <error reading> {pex.Message}");
                    }
                }
            }
            catch (Exception outer)
            {
                sb.AppendLine("Failed to build full diagnostics: " + outer);
            }

            return sb.ToString();
        }
    }
}
