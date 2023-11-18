using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Mono.Security.X509.X520;

namespace AicUtils
{
	public class FileFinder
	{
		static HashSet<string> duplicatedMd5 = new();
		public static void redirectObject(string name, Type type, ref UnityEngine.Object target)
		{
			UnityEngine.Object obj=getObject(name, type,target);
			if (obj == target) return;
			Console.WriteLine("Redirected " + name + " as " + type);
			target = obj;
		}
		public static UnityEngine.Object getObject(string name, Type type, UnityEngine.Object target)
		{
			if (name.Contains("/") || name.Contains("\\"))
				name = name.Substring(name.LastIndexOfAny("/\\".ToCharArray())+1);
			name=name.Replace(".bytes", "");
			if(type==typeof(Texture)||type==typeof(Texture2D))
			{
				byte[] b=readRedirectFile(name + ".png");
				if (b == null) return target;
				Texture2D rt = new Texture2D(0, 0);
				rt.LoadImage(b);
				rt.name = name;
				return rt;
			}
			if(type==typeof(TextAsset))
			{
				string trueName = name;
				if(target!=null)
				{
					string hash = md5(((TextAsset)target).bytes);
					if (duplicatedMd5.Contains(hash))
						trueName = "DUPLICATE"+hash.Substring(hash.Length-8) + "_" + name;
				}
				byte[] b = readRedirectFile(trueName);
				if(b==null) return target;
				string s = "[AicUtils_Base64]"+Convert.ToBase64String(b);
				TextAsset rt = new TextAsset(s);
				rt.name = name;
				return rt;
			}
			if(type==typeof(Sprite))
			{
				byte[] b = readRedirectFile(name + ".png");
				if(b==null) return target;
				(target as Sprite).texture.LoadImage(b);
				return target;
			}
			return target;
		}
		public static void unpack()
		{
			Directory.CreateDirectory("redirect");
			foreach(var o in Resources.LoadAll<TextAsset>("")) unpackTextAsset(o);
			foreach (var o in Resources.LoadAll<Texture>("")) unpackTexture(o);
			DirectoryInfo sa = new DirectoryInfo("AliceInCradle_Data/StreamingAssets");
			dfs(sa);
		}
		static void dfs(DirectoryInfo d)
		{
			foreach(FileInfo file in d.GetFiles())
			{
				if(file.FullName.EndsWith(".dat"))
				{
					AssetBundle b=AssetBundle.LoadFromFile(file.FullName);
					foreach (var o in b.LoadAllAssets<TextAsset>()) unpackTextAsset(o);
					foreach (var o in b.LoadAllAssets<Texture>()) unpackTexture(o);
					b.Unload(true);
				}
			}
			foreach(DirectoryInfo dir in d.GetDirectories())
				dfs(dir);
		}
		public static void unpackTextAsset(TextAsset o)
		{
			string name = o.name.Replace(".bytes","");
			string md = md5(o.bytes);
			if(duplicatedMd5.Contains(md))
				name= "DUPLICATE" + md.Substring(md.Length-8)+"_" + name;
			Console.WriteLine("unpack " + name);
			File.WriteAllBytes("redirect/" + name, o.bytes);
		}
		public static void unpackTexture(Texture o)
		{
			Console.WriteLine("unpack " + o.name);
			int w = o.width, h = o.height;
			RenderTexture rt = new(w, h, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit2(o, rt);

			RenderTexture original = RenderTexture.active;
			RenderTexture.active = rt;
			Texture2D t2d = new(w, h, TextureFormat.ARGB32, false);
			t2d.ReadPixels(new Rect(0, 0, w, h), 0, 0);
			RenderTexture.active = original;


			File.WriteAllBytes("redirect/" + o.name.Replace(".bytes","")+".png", t2d.EncodeToPNG());

		}
		public static byte[] readRedirectFile(string name)
		{
			string target = "redirect/" + name;
			try
			{
				return File.ReadAllBytes(target);
			}
			catch (Exception)
			{
				return null;
			}
		}

		internal static void handleDuplicates()
		{
			var res=Resources.LoadAll<TextAsset>("");
			Dictionary<string, List<TextAsset>> dict = new();
			foreach(var s in res)
			{
				if (!dict.ContainsKey(s.name))
					dict.Add(s.name, new());
				dict[s.name].Add(s);
			}
			foreach(var p in dict)
			{
				if(p.Value.Count()>1)
				{
					foreach(var o in p.Value)
					{
						duplicatedMd5.Add(md5(o.bytes));
					}
				}
			}
		}
		static string md5(byte[] bs)
		{
			HashAlgorithm ha = HashAlgorithm.Create("MD5");
			MemoryStream ms = new MemoryStream(bs);
			byte[] b = ha.ComputeHash(ms);
			ms.Close();
			string rt = "";
			foreach(byte c in b)
			{
				rt += hex(c / 16);
				rt += hex(c % 16);
			}
			return rt;
		}
		static char hex(int c)
		{
			return (char)(c < 10 ? '0' + c : 'a' - 10 + c );
		}
	}
}
