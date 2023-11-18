using BepInEx;
using BepInEx.Bootstrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AicUtils
{
	public static class CustomLoader
	{
		static void set(object instance,string field,object value)
		{
			PropertyInfo info = instance.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			info.SetValue(instance, value);
		}
		static object get(object instance,string field)
		{
			PropertyInfo info = instance.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return info.GetValue(instance);
		}
		public static void init(string modPath)
		{
			var pluginsToLoad=TypeLoader.FindPluginTypes(modPath,Chainloader.ToPluginInfo,null,"aiccustomloader");
			Console.WriteLine(string.Join("\n", pluginsToLoad));
			foreach (var keyValuePair in pluginsToLoad)
				foreach (var pluginInfo in keyValuePair.Value)
					//pluginInfo.Location = keyValuePair.Key;
					set(pluginInfo, "Location", keyValuePair.Key);
			var pluginInfos = pluginsToLoad.SelectMany(p => p.Value).ToList();
			var loadedAssemblies = new Dictionary<string, Assembly>();

			Console.WriteLine($"{pluginInfos.Count} plugin{(pluginInfos.Count == 1 ? "" : "s")} to load");

			// We use a sorted dictionary to ensure consistent load order
			var dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
			var pluginsByGUID = new Dictionary<string, PluginInfo>();

			foreach (var pluginInfoGroup in pluginInfos.GroupBy(info => info.Metadata.GUID))
			{
				PluginInfo loadedVersion = null;
				foreach (var pluginInfo in pluginInfoGroup.OrderByDescending(x => x.Metadata.Version))
				{
					if (loadedVersion != null)
					{
						Console.WriteLine($"Skipping [{pluginInfo}] because a newer version exists ({loadedVersion})");
						continue;
					}

					// Perform checks that will prevent loading plugins in this run
					var filters = pluginInfo.Processes.ToList();
					bool invalidProcessName = filters.Count != 0 && filters.All(x => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase));

					if (invalidProcessName)
					{
						Console.WriteLine($"Skipping [{pluginInfo}] because of process filters ({string.Join(", ", pluginInfo.Processes.Select(p => p.ProcessName).ToArray())})");
						continue;
					}

					loadedVersion = pluginInfo;
					dependencyDict[pluginInfo.Metadata.GUID] = pluginInfo.Dependencies.Select(d => d.DependencyGUID);
					pluginsByGUID[pluginInfo.Metadata.GUID] = pluginInfo;
				}
			}

			foreach (var pluginInfo in pluginsByGUID.Values.ToList())
			{
				if (pluginInfo.Incompatibilities.Any(incompatibility => pluginsByGUID.ContainsKey(incompatibility.IncompatibilityGUID)))
				{
					pluginsByGUID.Remove(pluginInfo.Metadata.GUID);
					dependencyDict.Remove(pluginInfo.Metadata.GUID);

					var incompatiblePlugins = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGUID).Where(x => pluginsByGUID.ContainsKey(x)).ToArray();
					string message = $@"Could not load [{pluginInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
					//DependencyErrors.Add(message);
					Console.WriteLine(message);
				}
			}

			var emptyDependencies = new string[0];

			// Sort plugins by their dependencies.
			// Give missing dependencies no dependencies of its own, which will cause missing plugins to be first in the resulting list.
			var sortedPlugins = Utility.TopologicalSort(dependencyDict.Keys, x => dependencyDict.TryGetValue(x, out var deps) ? deps : emptyDependencies).ToList();

			var invalidPlugins = new HashSet<string>();
			var processedPlugins = new Dictionary<string, Version>();


			foreach (var pluginGUID in sortedPlugins)
			{
				// If the plugin is missing, don't process it
				if (!pluginsByGUID.TryGetValue(pluginGUID, out var pluginInfo))
					continue;

				var dependsOnInvalidPlugin = false;
				var missingDependencies = new List<BepInDependency>();
				foreach (var dependency in pluginInfo.Dependencies)
				{
					bool IsHardDependency(BepInDependency dep) => (dep.Flags & BepInDependency.DependencyFlags.HardDependency) != 0;

					// If the dependency wasn't already processed, it's missing altogether
					bool dependencyExists = processedPlugins.TryGetValue(dependency.DependencyGUID, out var pluginVersion);
					if (!dependencyExists || pluginVersion < dependency.MinimumVersion)
					{
						// If the dependency is hard, collect it into a list to show
						if (IsHardDependency(dependency))
							missingDependencies.Add(dependency);
						continue;
					}

					// If the dependency is invalid (e.g. has missing dependencies) and hard, report that to the user
					if (invalidPlugins.Contains(dependency.DependencyGUID) && IsHardDependency(dependency))
					{
						dependsOnInvalidPlugin = true;
						break;
					}
				}

				processedPlugins.Add(pluginGUID, pluginInfo.Metadata.Version);

				if (dependsOnInvalidPlugin)
				{
					string message = $"Skipping [{pluginInfo}] because it has a dependency that was not loaded. See previous errors for details.";
					//DependencyErrors.Add(message);
					Console.WriteLine(message);
					continue;
				}

				if (missingDependencies.Count != 0)
				{
					bool IsEmptyVersion(Version v) => v.Major == 0 && v.Minor == 0 && v.Build <= 0 && v.Revision <= 0;

					string message = $@"Could not load [{pluginInfo}] because it has missing dependencies: {string.Join(", ", missingDependencies.Select(s => IsEmptyVersion(s.MinimumVersion) ? s.DependencyGUID : $"{s.DependencyGUID} (v{s.MinimumVersion} or newer)").ToArray())}";
					//DependencyErrors.Add(message);
					Console.WriteLine(message);

					invalidPlugins.Add(pluginGUID);
					continue;
				}

				try
				{
					Console.WriteLine($"CustomLoader: Loading [{pluginInfo}]");

					if (!loadedAssemblies.TryGetValue(pluginInfo.Location, out var ass))
						loadedAssemblies[pluginInfo.Location] = ass = Assembly.LoadFile(pluginInfo.Location);

					//Chainloader.PluginInfos[pluginGUID] = pluginInfo;
					//pluginInfo.Instance = (BaseUnityPlugin)ManagerObject.AddComponent(ass.GetType(pluginInfo.TypeName));
					var ins= (BaseUnityPlugin)Chainloader.ManagerObject.AddComponent(ass.GetType((string)get(pluginInfo,"TypeName")));

					//_plugins.Add(pluginInfo.Instance);
				}
				catch (Exception ex)
				{
					invalidPlugins.Add(pluginGUID);
				}
			}
		}
	}
}
