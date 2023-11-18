using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AicUtils
{
	public class VersionChecker
	{
		public static readonly int currentVersion = 2;
		public class Config
		{
			public int assetsVersion=-1;
			public string aicVersion=null;
			public Config()
			{

			}
			public Config(bool mark)
			{
				assetsVersion = currentVersion;
				aicVersion = UnityEngine.Application.version;
			}
		}
		public static void checkVersion()
		{
			try
			{
				if (!Directory.Exists("redirect"))
					throw new Exception("没有重定向文件夹。\n请在\"AliceInCradle.exe\"同目录下创建文件\"unpackall\"，\n我们将在下次启动自动为你解包。");
				byte[] b=File.ReadAllBytes("redirect/aicutils_config.yml");
				var d=new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
				var c=d.Deserialize<Config>(UTF8Encoding.UTF8.GetString
					((b)));
				if (!(c.assetsVersion == currentVersion))
					throw new Exception("资源版本过低。\n请在备份好redirect目录后在\"AliceInCradle.exe\"同目录下创建文件\"unpackall\"，\n我们将在下次启动自动为你解包。");
				if (!(c.aicVersion == UnityEngine.Application.version))
					throw new Exception("AIC版本与资源版本不匹配。\n请在备份好redirect目录后在\"AliceInCradle.exe\"同目录下创建文件\"unpackall\"，\n我们将在下次启动自动为你解包。");
				//pass
			}
			catch(Exception e)
			{
				//UnityEngine.Application.Quit(3);
				throw e;
			}
		}
		public static void saveVersion(Config c)
		{
			var s=new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).JsonCompatible().Build();
			var b=s.Serialize(c);
			File.WriteAllBytes("redirect/aicutils_config.yml", UTF8Encoding.UTF8.GetBytes(b));
		}
	}
}
