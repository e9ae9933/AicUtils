using BepInEx;
using HarmonyLib;
using m2d;
using nel;
using PixelLiner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using UnityEngine;
using XX;
using YamlDotNet.Serialization;
using static evt.EV;

namespace AicUtils;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private void Update()
    {
	}
    static Dictionary<string, Dictionary<string, Dictionary<string, string[]>>> ev;
	private void Awake()
    {
        try
        {
            X.dl("[AicUtils] Here we are");
            Console.WriteLine("Loading dlls...?");
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Console.WriteLine("handle duplicates");
            FileFinder.handleDuplicates();

            if(!Directory.Exists("redirect"))
            {
                bool ok=question("看起来这是AicUtils第一次被启用。\n需要为你自动解包吗？\n这可能花费几分钟。");
                if (ok)
                    File.Create("unpackall");
            }
            if (File.Exists("unpackall"))
            {
                try
                {
                    FileFinder.unpack();
                    VersionChecker.saveVersion(new VersionChecker.Config(true));
                    File.Delete("unpackall");
                    info("解包完成。");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    crash(e);
                }
            }
            VersionChecker.checkVersion();

            Console.WriteLine("added event");

            Harmony.CreateAndPatchAll(GetType());
            //load translations
            reloadEventTranslations();
            runCheck();

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }
        catch(Exception e)
        {
            X.dl(e.ToString());
            Console.WriteLine(e.ToString());
            try
            {
                crash(e);
            }
            catch (Exception)
            {
                Console.WriteLine("Sleeping 5000ms");
                Thread.Sleep(5000);
            }
			UnityEngine.Application.Quit(1);
        }
    }
    public static void crash(Exception e)
	{
		StringBuilder sb = new();
		sb.AppendLine("检测到了一个问题，因此AIC即将关闭来防止进一步的损失。");
        sb.AppendLine();
		sb.AppendLine(e.Message);
		sb.AppendLine();
		sb.AppendLine("如果这是你第一次看见这个错误提示，尝试重新启动AliceInCradle。如果这个错误提示再次出现，跟随这些步骤：");
		sb.AppendLine();
		sb.AppendLine("检查AliceInCradle.exe的同目录下是否存在redirect文件夹。如果不存在，请在同目录下新建文件\"unpackall\"，我们将在下一次运行时为你自动解包。");
		sb.AppendLine();
		sb.AppendLine("如果你最近更新了新的AIC版本或者AicUtils版本，请尝试备份redirect文件夹，然后同目录下新建文件\"unpackall\"，我们将在下一次运行时重新解包。");
		sb.AppendLine();
		sb.AppendLine("或者，你还可以和我们联系。");
		sb.AppendLine();
		sb.AppendLine("技术详细信息：");
		sb.AppendLine();
		sb.AppendLine("*** EXCEPTION: " + e.GetType().Name);
		sb.AppendLine();
		sb.AppendLine(e.ToString());
		error(sb.ToString());
	}
    public static void runCheck()
    {
        if(!Directory.Exists("redirect/l10n"))
        {
            warning("没有自定义的翻译文件夹。\n将会使用原生翻译。\n请尝试AicToolbox来获取自定义翻译。");
            return;
		}
		if (!Directory.GetFiles("redirect/l10n").Any(y => y.EndsWith(".yml")))
		{
			warning("没有自定义的文本翻译。\n将会使用原生翻译。\n请尝试AicToolbox来获取自定义翻译。");
		}
        else
        {
            TextAsset ta = Resources.Load<TextAsset>("__tx_list");
            if (!ta.text.Trim().EndsWith("_redirect"))
                warning("存在自定义文本翻译但是没有被启用。\n请尝试在__tx_list的最尾部添加\"_redirect\"来启用自定义文本翻译。\n应当放在最尾部。");
        }
        if(Directory.GetDirectories("redirect/l10n").Length==0)
		{
			warning("没有自定义的事件翻译。\n将会使用原生翻译。\n请尝试AicToolbox来获取自定义翻译。");
		}
	}
    [MethodImpl(MethodImplOptions.NoInlining|MethodImplOptions.NoOptimization)]
	public static void info(string s)
	{
		MessageBox.Show(s, "信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void warning(string s)
	{
		MessageBox.Show(s, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
	}
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void error(string s)
    {
        MessageBox.Show(s, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
	}
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static bool question(string s)
	{
		var result=MessageBox.Show(s, "问题", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        return result == DialogResult.Yes;
	}

	private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
	{
        Console.WriteLine("Requesting "+args.Name);
        if (args.Name.Split(',')[0].Equals("YamlDotNet"))
        {
            byte[] b = Dlls.YamlDotNet;
            Console.WriteLine("tried to load YamlDotNet..."+b.Length);
            return Assembly.Load(b);
		}
		if (args.Name.Split(',')[0].Equals("System.Windows.Forms"))
		{
			byte[] b = Dlls.System_Windows_Forms_2;
			Console.WriteLine("tried to load Forms..."+b.Length);
			return Assembly.Load(b);
		}
		Console.WriteLine("failed " + args.Name);
        return null;
	}

	[HarmonyPatch(typeof(TextAsset),"bytes",MethodType.Getter)]
    [HarmonyPostfix]
    public static void onBytes(ref byte[] __result)
    {
        string s = Encoding.UTF8.GetString(__result);
        if(s.StartsWith("[AicUtils_Base64]"))
        {
            string ss = s.Substring(17);
            __result=Convert.FromBase64String(ss);
        }
	}
	[HarmonyPatch(typeof(AssetBundle), "LoadAsset", new Type[] { typeof(string), typeof(Type) })]
	[HarmonyPostfix]
	public static void onBundleLoad(string name, Type type, ref UnityEngine.Object __result)
	{
		FileFinder.redirectObject(name, type, ref __result);
	}

	[HarmonyPatch(typeof(Resources), "Load",new Type[]{typeof(string),typeof(Type)})]
    [HarmonyPostfix]
    public static void onLoad(string path,Type systemTypeInstance, ref UnityEngine.Object __result)
    {
        FileFinder.redirectObject(path, systemTypeInstance, ref __result);
	}
    [HarmonyPatch(typeof(NKT),"readStreamingText")]
    [HarmonyPostfix]
    public static void onText(string path,ref string __result)
    {
        if(path.EndsWith("_redirect.txt"))
        {
            int index=path.LastIndexOfAny("/\\".ToCharArray());
            string name=path.Substring(index+1, path.Length - index - 14);
            Console.WriteLine("loading text " + name);
            try
            {
                __result = Encoding.UTF8.GetString(File.ReadAllBytes("redirect/l10n/" + name + ".yml"));
            }
            catch(Exception)
            {
                Console.WriteLine("read " + path + " failed");
                return;
            }
            Console.WriteLine("read file in UTF-8 with length " + __result.Length);
        }
	}
    [HarmonyPatch(typeof(TX),"readTexts")]
    [HarmonyPrefix]
    public static bool readTexts(string LT,TX.TXFamily Fam,CsvVariableContainer VarCon)
	{
		string str = LT;
        if (str == null) return false;
		try
		{
            var deserializer = new DeserializerBuilder().Build();
            Dictionary<string, string> map = deserializer.Deserialize<Dictionary<string, string>>(str);
            Console.WriteLine("Deserialized into " + map.Count);
            foreach (var entry in map)
            {
                string key = entry.Key;
                string value = entry.Value;
                //Console.WriteLine("fixed " + key + ": " + value);
                if (key.StartsWith("%"))
                {
                    TX tx = null;
                    string fix = key + " " + value;
                    CsvReader csvReader = new CsvReader(fix, new Regex("[ \\s\\t]+"), false)
                    {
                        no_write_varcon = 2,
                        no_replace_quote = true,
                        VarCon = VarCon
                    };
                    csvReader.read();
                    bool rt = NEL.readLocalizeTxItemScript(csvReader, Fam, ref tx);
                    if (!rt)
                    {
                        Console.WriteLine("Failed on " + key + " " + value);
                    }
                }
                else
                {
                    TX tx = TX.getTX(key.StartsWith("&&")?key.Substring(2):key, false, false, Fam);
                    tx.replaceTextContents(value);
                }
            }
            Console.WriteLine("Read text complete: " + Fam.key);
        }
        catch (Exception)
		{
            //Console.WriteLine("Read text failed: " + Fam.key+" falling back");
            return true;
        }
        return false;
    }
    void reloadEventTranslations()
    {
        ev = new ();
        DirectoryInfo l10n = new DirectoryInfo("redirect/l10n");
        if(!l10n.Exists)
        {
            Console.WriteLine(" no l10n found");
            return;
        }
        Console.WriteLine("Loading events from " + l10n);
        foreach(DirectoryInfo dir in l10n.GetDirectories())
        {
            Console.WriteLine("Loading lang " + dir);
            string lang=dir.Name;
            Dictionary<string, Dictionary<string, string[]>> target=new();
            ev.Add(lang, target);
			foreach (FileInfo file in dir.GetFiles())
            {
                try
                {
                    string yml = File.ReadAllText(file.FullName, Encoding.UTF8);
                    var deserializer = new DeserializerBuilder().Build();
                    var from = deserializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(yml);
                    foreach (var pair in from)
                    {
                        string key = pair.Key;
                        if (!target.ContainsKey(key)) target.Add(key, new());
                        var d = target.GetValueSafe(key);
                        var e = pair.Value.GetEnumerator();
                        while (e.MoveNext())
                        {
                            var p = e.Current;
                            var k = p.Key;
                            var v = p.Value;
                            d.Add(k, v);
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Failed loading " + file);
                    Console.WriteLine(e);
                    warning("读取事件翻译 " + file.Name + " 出错。\n位置：" + file.FullName + "\n" + e.ToString());
                }
			}
        }
    }
    [HarmonyPatch(typeof(NelMSGResource),"getContent")]
    [HarmonyPrefix]
    public static bool onContent(string label,ref string[] __result)
    {
        try
        {
            //Console.WriteLine(Environment.StackTrace);
            string lang=NelMSGResource.load_lang;
            string k1 = label.Split(' ')[0];
            string k2 = label.Split(' ')[1];
            if (!ev[lang][k1].ContainsKey(k2))
                throw new Exception("No k2: "+k2);
            string[] ans=ev[lang][k1][k2];
            string[] rt = new string[ans.Length];
            for (int i = 0; i < ans.Length; i++)
                rt[i] = ans[i].Clone() as string;
            Console.WriteLine("label " + label + " returned " + string.Join("",rt));
            __result = rt;
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            string s = e.ToString();
            __result = new string[] { "Load failed: " + label+"\n"+"Caused by: "+e.GetType().Name+"\n"+e.Message};
        }
        return false;
    }
}
