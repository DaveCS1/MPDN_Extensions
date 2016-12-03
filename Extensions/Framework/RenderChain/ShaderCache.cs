// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Mpdn.OpenCl;
using Mpdn.RenderScript;
using SharpDX;

namespace Mpdn.Extensions.Framework.RenderChain
{
    public static class ShaderCache
    {
        private static bool s_Loaded;

        public static string ShaderPathRoot
        {
            get
            {
                var asmPath = typeof (IRenderScript).Assembly.Location;
#if DEBUG
                return Path.GetFullPath(Path.Combine(PathHelper.GetDirectoryName(asmPath), "..\\", "Extensions", "RenderScripts"));
#else
                return Path.Combine(PathHelper.GetDirectoryName(asmPath), "Extensions", "RenderScripts");
#endif
            }
        }

        private static class Cache<T> where T : class, IShaderBase
        {
            private static readonly Dictionary<string, ShaderWithDateTime> s_LoadedShaders =
                new Dictionary<string, ShaderWithDateTime>();

            private static Dictionary<string, ShaderWithDateTime> s_CompiledShaders =
                new Dictionary<string, ShaderWithDateTime>();

            private static bool s_Saved = false;

            public static string ShaderCacheRoot
            {
                get { return AppPath.GetUserDataDir("ShaderCache"); }
            }

            public static T AddLoaded(string shaderPath, Func<string, T> loadFunc)
            {
                var lastMod = File.GetLastWriteTimeUtc(shaderPath);

                ShaderWithDateTime entry;
                if (s_LoadedShaders.TryGetValue(shaderPath, out entry) &&
                    entry.LastModified == lastMod)
                {
                    return entry.Shader;
                }

                if (entry != null)
                {
                    DisposeHelper.Dispose(entry.Shader);
                    s_LoadedShaders.Remove(shaderPath);
                }

                var shader = loadFunc(shaderPath);
                s_LoadedShaders.Add(shaderPath, new ShaderWithDateTime(shader, lastMod, false));
                return shader;
            }

            public static T AddCompiled(string shaderPath, string key, Func<T> compileFunc, Func<string, T> loadFunc)
            {
                var lastMod = File.GetLastWriteTimeUtc(shaderPath);
                T shader = null;

                ShaderWithDateTime entry;
                if (s_CompiledShaders.TryGetValue(key, out entry) && entry.LastModified == lastMod)
                {
                    if (entry.Shader != null)
                        return entry.Shader;

                    try
                    {
                        if (loadFunc != null)
                        {
                            shader = loadFunc(entry.CachePath);
                        }
                    }
                    catch
                    {
                        // Recompile if we encounter an error
                    }
                }

                if (shader == null)
                {
                    try
                    {
                        shader = compileFunc();
                    }
                    catch (CompilationException e)
                    {
                        throw new CompilationException(e.ResultCode, "Compilation Error in " + key + "\r\n\r\n" + e.Message);
                    }
                    catch (OpenClException e)
                    {
                        throw new OpenClException("Compilation Error in " + key + "\r\n\r\n" + e.Message, e.ErrorCode);
                    }
                    s_Saved = false;

                    // Remove obsolete cache file
                    if (entry != null && loadFunc != null)
                    {
                        File.Delete(entry.CachePath);
                    }
                }

                // Save / Replace Entry
                if (entry != null)
                {
                    DisposeHelper.Dispose(entry);
                    s_CompiledShaders.Remove(key);
                }

                s_CompiledShaders.Add(key, new ShaderWithDateTime(shader, lastMod, loadFunc != null));
                return shader;
            }


            [Serializable]
            private class ShaderWithDateTime : IDisposable
            {
                private readonly DateTime m_LastModified;
                private readonly string m_CachePath;

                [NonSerialized]
                private readonly T m_Shader;

                public T Shader
                {
                    get { return m_Shader; }
                }

                public DateTime LastModified
                {
                    get { return m_LastModified; }
                }

                public string CachePath
                {
                    get { return m_CachePath; }
                }

                public ShaderWithDateTime(T shader, DateTime lastModified, bool saveByteCode)
                {
                    m_Shader = shader;
                    m_LastModified = lastModified;
                    if (!saveByteCode)
                        return;

                    do
                    {
                        m_CachePath = Path.Combine(AppPath.GetUserDataDir("ShaderCache"),
                            string.Format("{0}.{1}", Guid.NewGuid(), "cso"));
                    } while (File.Exists(m_CachePath));
                    Directory.CreateDirectory(PathHelper.GetDirectoryName(m_CachePath));
                    File.WriteAllBytes(m_CachePath, shader.ObjectByteCode);
                }

                public void Dispose()
                {
                    DisposeHelper.Dispose(Shader);
                }
            }

            public static void Save(string path)
            {
                if (s_Saved)
                    return;

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    var bf = new BinaryFormatter();
                    bf.Serialize(fs, s_CompiledShaders);
                }
                s_Saved = true;
            }

            public static void Load(string path)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var bf = new BinaryFormatter();
                        s_CompiledShaders = (Dictionary<string, ShaderWithDateTime>) bf.Deserialize(fs);
                    }
                    s_Saved = true; // Everything already on disk
                }
                catch
                {
                    // Ignore errors if we can't load
                }
            }
        }

        private static string ShaderCachePath
        {
            get { return Path.Combine(Cache<IShader>.ShaderCacheRoot, "ShaderIndex.dat"); }
        }

        private static string Shader11CachePath
        {
            get { return Path.Combine(Cache<IShader11>.ShaderCacheRoot, "Shader11Index.dat"); }
        }

        public static void Load()
        {
            if (s_Loaded)
                return;

            s_Loaded = true;
            Cache<IShader>.Load(ShaderCachePath);
            Cache<IShader11>.Load(Shader11CachePath);
        }

        public static string GetRelativePath(string rootPath, string filename)
        {
            if (!Path.IsPathRooted(filename))
                return filename;

            if (!filename.StartsWith(rootPath))
                return filename;

            return filename.Remove(0, rootPath.Length + 1);
        }

        private static string GetRelative(string shaderFileName)
        {
            return GetRelativePath(ShaderPathRoot, Path.GetFullPath(shaderFileName));
        }

        public static IShader CompileShader(string shaderFileName, string profile = "ps_3_0", string entryPoint = "main", string macroDefinitions = null)
        {
            var result = Cache<IShader>.AddCompiled(shaderFileName,
                string.Format("\"{0}\" /E {1} /T {2} /D {3}", GetRelative(shaderFileName), entryPoint, profile, macroDefinitions),
                () => Renderer.CompileShader(shaderFileName, entryPoint, profile, macroDefinitions),
                Renderer.LoadShader);

            Cache<IShader>.Save(ShaderCachePath);
            return result;
        }

        public static IShader11 CompileShader11(string shaderFileName, string profile, string entryPoint = "main", string macroDefinitions = null)
        {
            var result = Cache<IShader11>.AddCompiled(shaderFileName,
                string.Format("\"{0}\" /E {1} /T {2} /D {3}", GetRelative(shaderFileName), entryPoint, profile, macroDefinitions),
                () => Renderer.CompileShader11(shaderFileName, entryPoint, profile, macroDefinitions),
                Renderer.LoadShader11);

            Cache<IShader11>.Save(Shader11CachePath);
            return result;
        }

        public static IKernel CompileClKernel(string sourceFileName, string entryPoint, string options = null)
        {
            return Cache<IKernel>.AddCompiled(sourceFileName,
                string.Format("\"{0}\" /E {1} /Opts {2}", GetRelative(sourceFileName), entryPoint, options),
                () => Renderer.CompileClKernel(sourceFileName, entryPoint, options),
                null);
        }

        public static IShader LoadShader(string shaderFileName)
        {
            return Cache<IShader>.AddLoaded(shaderFileName, Renderer.LoadShader);
        }

        public static IShader11 LoadShader11(string shaderFileName)
        {
            return Cache<IShader11>.AddLoaded(shaderFileName, Renderer.LoadShader11);
        }
    }
}