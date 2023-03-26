using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace YARG.Settings {
	public static partial class SettingsManager {
		private class SettingLocation : Attribute {
			public string location;
			public int order;

			public SettingLocation(string location, int order) {
				this.location = location;
				this.order = order;
			}
		}

		private class SettingType : Attribute {
			public string type;

			public SettingType(string type) {
				this.type = type;
			}
		}

		private class SettingInteractableFunc : Attribute {
			public string settingName;

			public SettingInteractableFunc(string settingName) {
				this.settingName = settingName;
			}
		}

		private class SettingChangeFunc : Attribute {
			public string settingName;

			public SettingChangeFunc(string settingName) {
				this.settingName = settingName;
			}
		}

		private class SettingButton : Attribute {
			public string settingName;

			public SettingButton(string settingName) {
				this.settingName = settingName;
			}
		}

		private class SettingSpace : Attribute {

		}

		public struct SettingInfo {
			public string name;
			public string location;
			public string type;
			public bool spaceAbove;
		}

		private static SettingContainer settingsContainer;

		private static OrderedDictionary settings = new();
		private static Dictionary<string, MethodInfo> interactableFuncs = new();
		private static Dictionary<string, MethodInfo> changeFuncs = new();

		static SettingsManager() {
			SortedDictionary<int, object> settingsWithLocation = new();

			// Get all setting fields ordered by SettingLocation
			var type = typeof(SettingContainer);
			var fields = type.GetFields();
			foreach (var field in fields) {
				var attributes = field.GetCustomAttributes(false);
				foreach (var attribute in attributes) {
					if (attribute is SettingLocation location) {
						settingsWithLocation.Add(location.order, field);
					}
				}
			}

			// Do the same with methods with the SettingButton attribute
			var methods = type.GetMethods();
			foreach (var method in methods) {
				var attributes = method.GetCustomAttributes(false);

				SettingButton button = null;
				SettingLocation location = null;

				foreach (var attribute in attributes) {
					if (attribute is SettingButton buttonAttrib) {
						button = buttonAttrib;
					}

					if (attribute is SettingLocation locationAttrib) {
						location = locationAttrib;
					}
				}

				if (button == null || location == null) {
					continue;
				}

				settingsWithLocation.Add(location.order, method);
			}

			// Convert the dictionary to a list
			foreach (var (_, obj) in settingsWithLocation) {
				if (obj is FieldInfo field) {
					settings.Add(field.Name, obj);
				} else if (obj is MethodInfo method) {
					// Get name from SettingButton attribute
					var attributes = method.GetCustomAttributes(false);
					foreach (var attribute in attributes) {
						if (attribute is SettingButton button) {
							settings.Add(button.settingName, obj);
							break;
						}
					}
				}
			}

			// Get all interactable functions
			foreach (var method in methods) {
				var attributes = method.GetCustomAttributes(false);
				foreach (var attribute in attributes) {
					if (attribute is SettingInteractableFunc interactableFunc) {
						interactableFuncs.Add(interactableFunc.settingName, method);
					}
				}
			}

			// Get all change functions
			foreach (var method in methods) {
				var attributes = method.GetCustomAttributes(false);
				foreach (var attribute in attributes) {
					if (attribute is SettingChangeFunc changeFunc) {
						changeFuncs.Add(changeFunc.settingName, method);
					}
				}
			}

			// Create settings container
			try {
				var path = Path.Combine(Application.persistentDataPath, "settings.json");
				settingsContainer = JsonConvert.DeserializeObject<SettingContainer>(File.ReadAllText(path));
			} catch (Exception) { }

			// If failed or the settings don't exist, just create new settings
			if (settingsContainer == null) {
				settingsContainer = new SettingContainer();

				// Get song folder location
				var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				var cloneHeroPath = Path.Combine(documentsPath, $"Clone Hero{Path.DirectorySeparatorChar}Songs");
				var yargPath = Path.Combine(documentsPath, $"YARG{Path.DirectorySeparatorChar}Songs");

				string songFolder = yargPath;
				if (Directory.Exists(cloneHeroPath)) {
					songFolder = cloneHeroPath;
				} else if (!Directory.Exists(yargPath)) {
					Directory.CreateDirectory(yargPath);
				}

				// Set the song folder location
				settingsContainer.songFolder = songFolder;
			}
		}

		private static void SaveSettings() {
			var path = Path.Combine(Application.persistentDataPath, "settings.json");
			File.WriteAllText(path, JsonConvert.SerializeObject(settingsContainer));
		}

		public static SettingInfo[] GetAllSettings() {
			var settingInfos = new SettingInfo[settings.Count];

			int i = 0;
			foreach (var key in settings.Keys) {
				var name = (string) key;

				if (settings[name] is FieldInfo field) {
					// If this is a field setting...

					var attributes = field.GetCustomAttributes(false);

					SettingLocation location = null;
					SettingType type = null;
					SettingSpace space = null;

					// Get location and type
					foreach (var attribute in attributes) {
						if (attribute is SettingLocation locationAttrib) {
							location = locationAttrib;
						}

						if (attribute is SettingType typeAttrib) {
							type = typeAttrib;
						}

						if (attribute is SettingSpace spaceAttrib) {
							space = spaceAttrib;
						}
					}

					settingInfos[i] = new SettingInfo {
						name = name,
						location = location.location,
						type = type.type,
						spaceAbove = space != null
					};
				} else if (settings[name] is MethodInfo method) {
					// If this is a button...

					var attributes = method.GetCustomAttributes(false);

					SettingLocation location = null;
					SettingSpace space = null;

					// Get location and type
					foreach (var attribute in attributes) {
						if (attribute is SettingLocation locationAttrib) {
							location = locationAttrib;
						}

						if (attribute is SettingSpace spaceAttrib) {
							space = spaceAttrib;
						}
					}

					settingInfos[i] = new SettingInfo {
						name = name,
						location = location.location,
						type = "Button",
						spaceAbove = space != null
					};
				}

				i++;
			}

			return settingInfos;
		}

		public static object GetSettingValue(string name) {
			if (!settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} does not exist!");
				return null;
			}

			if (settings[name] is FieldInfo field) {
				return field.GetValue(settingsContainer);
			}

			Debug.LogWarning($"Setting {name} is not a field!");
			return null;
		}

		public static T GetSettingValue<T>(string name) {
			var value = GetSettingValue(name);
			if (value is T t) {
				return t;
			} else if (value == null) {
				return default;
			}

			Debug.LogWarning($"Setting {name} is not of type {typeof(T)}!");
			return default;
		}

		public static void SetSettingValue(string name, object value) {
			if (!settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} does not exist!");
				return;
			}

			if (settings[name] is FieldInfo field) {
				field.SetValue(settingsContainer, value);

				if (changeFuncs.ContainsKey(name)) {
					changeFuncs[name].Invoke(settingsContainer, null);
				}

				SaveSettings();
				return;
			}

			Debug.LogWarning($"Setting {name} is not a field!");
		}

		public static void InvokeButtonAction(string name) {
			if (!settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} does not exist!");
				return;
			}

			if (settings[name] is MethodInfo method) {
				method.Invoke(settingsContainer, null);
				return;
			}

			Debug.LogWarning($"Setting {name} is not a method!");
		}

		public static bool IsSettingInteractable(string name) {
			if (!settings.Contains(name)) {
				Debug.LogWarning($"Setting {name} does not exist!");
				return false;
			}

			if (interactableFuncs.ContainsKey(name)) {
				return (bool) interactableFuncs[name].Invoke(settingsContainer, null);
			}

			return true;
		}
	}
}