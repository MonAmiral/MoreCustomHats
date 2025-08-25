using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace MoreCustomHats
{
	[BepInPlugin("monamiral.morecustomhats", "More custom hats", "1.0.0")]
	public class Plugin : BaseUnityPlugin
	{
		public static AssetBundle assetBundle;
		public static List<HatEntry> hats;

		private class Patcher
		{
			private static readonly int SkinColor = Shader.PropertyToID("_SkinColor");

			public static bool CreateHatOption(Customization customization, string name, Texture2D icon)
			{
				if (Array.Exists(customization.hats, hat => hat.name == name))
				{
					Debug.LogError($"[MonAmiral] Trying to add {name} a second time.");
					return false;
				}

				CustomizationOption hatOption = ScriptableObject.CreateInstance<CustomizationOption>();
				hatOption.color = Color.white;
				hatOption.name = name;
				hatOption.texture = icon;
				hatOption.type = Customization.Type.Hat;
				hatOption.requiredAchievement = ACHIEVEMENTTYPE.NONE;
				customization.hats = customization.hats.AddToArray(hatOption);

				Debug.Log($"[MonAmiral] {name} added.");

				return true;
			}

			[HarmonyPatch(typeof(PassportManager), "Awake")]
			[HarmonyPostfix]
			public static void PassportManagerAwakePostfix(PassportManager __instance)
			{
				Customization customization = __instance.GetComponent<Customization>();

				Debug.Log($"[MonAmiral] Adding hat CustomizationOptions.");
				for (int i = 0; i < hats.Count; i++)
				{
					HatEntry hat = hats[i];
					CreateHatOption(customization, hat.Name, hat.Icon);
				}

				Debug.Log($"[MonAmiral] Done.");
			}

			[HarmonyPatch(typeof(CharacterCustomization), "Awake")]
			[HarmonyPostfix]
			public static void CharacterCustomizationAwakePostfix(CharacterCustomization __instance)
			{
				Transform hatsContainer = __instance.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(1).GetChild(1);

				Debug.Log($"[MonAmiral] Instanciating CharacterCustomization hats as children of {__instance} / {hatsContainer}.");
				for (int i = 0; i < hats.Count; i++)
				{
					HatEntry hat = hats[i];

					GameObject hatInstance = GameObject.Instantiate(hat.Prefab, hatsContainer.position, hatsContainer.rotation, hatsContainer);
					SetLayerRecursively(hatInstance.transform, hatsContainer.gameObject.layer);

					Renderer renderer = hatInstance.GetComponentInChildren<Renderer>();
					renderer.gameObject.SetActive(false);

					for (int j = 0; j < renderer.materials.Length; j++)
					{
						renderer.materials[j].shader = Shader.Find("W/Character");
					}

					Debug.Log($"[MonAmiral] CharacterCustomization Hats[{__instance.refs.playerHats.Length}] = {hat.Name}");
					__instance.refs.playerHats = __instance.refs.playerHats.AddToArray(renderer);
					__instance.refs.AllRenderers = __instance.refs.AllRenderers.AddToArray(renderer);
				}
			}

			private static PlayerCustomizationDummy lastModifiedDummy;

			[HarmonyPatch(typeof(PlayerCustomizationDummy), "UpdateDummy")]
			[HarmonyPrefix]
			public static void PlayerCustomizationDummyUpdateDummyPrefix(PlayerCustomizationDummy __instance)
			{
				if (__instance == lastModifiedDummy)
				{
					return;
				}

				lastModifiedDummy = __instance;

				Transform hatsContainer = __instance.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(1).GetChild(1);

				Debug.Log($"[MonAmiral] Instanciating PlayerCustomizationDummy hats as children of {__instance} / {hatsContainer}.");
				for (int i = 0; i < hats.Count; i++)
				{
					HatEntry hat = hats[i];

					GameObject hatInstance = GameObject.Instantiate(hat.Prefab, hatsContainer.position, hatsContainer.rotation, hatsContainer);
					SetLayerRecursively(hatInstance.transform, hatsContainer.gameObject.layer);

					Renderer renderer = hatInstance.GetComponentInChildren<Renderer>();
					renderer.gameObject.SetActive(false);

					for (int j = 0; j < renderer.materials.Length; j++)
					{
						renderer.materials[j].shader = Shader.Find("W/Character");
					}

					Debug.Log($"[MonAmiral] PlayerCustomizationDummy Hats[{__instance.refs.playerHats.Length}] = {hat.Name}");
					__instance.refs.playerHats = __instance.refs.playerHats.AddToArray(renderer);
				}
			}

			[HarmonyPatch(typeof(CharacterCustomization), "OnPlayerDataChange")]
			[HarmonyPostfix]
			public static void CharacterCustomizationOnPlayerDataChangePostFix(CharacterCustomization __instance, ref PersistentPlayerData playerData)
			{
				if (__instance.refs.PlayerRenderers[0] == null/* || __instance._character.isBot*/)
				{
					return;
				}

				int currentSkin = playerData.customizationData.currentSkin;
				if (__instance.useDebugColor)
				{
					currentSkin = __instance.debugColorIndex;
				}

				Color color = Singleton<Customization>.Instance.skins[currentSkin].color;

				for (int i = 0; i < __instance.refs.playerHats.Length; i++)
				{
					for (int j = 0; j < __instance.refs.playerHats[i].materials.Length; j++)
					{
						__instance.refs.playerHats[i].materials[j].SetColor(SkinColor, color);
					}
				}
			}

			[HarmonyPatch(typeof(PlayerCustomizationDummy), "SetPlayerColor")]
			[HarmonyPostfix]
			public static void PlayerCustomizationDummySetPlayerColorPostFix(PlayerCustomizationDummy __instance, ref int index)
			{
				if (index <= Singleton<Customization>.Instance.skins.Length)
				{
					Color color = Singleton<Customization>.Instance.skins[index].color;

					for (int i = 0; i < __instance.refs.playerHats.Length; i++)
					{
						for (int j = 0; j < __instance.refs.playerHats[i].materials.Length; j++)
						{
							__instance.refs.playerHats[i].materials[j].SetColor(SkinColor, color);
						}
					}
				}
			}
		}

		public void Awake()
		{
			new Harmony("monamiral.morecustomhats").PatchAll(typeof(Patcher));
			this.StartCoroutine(LoadHatsFromDisk());
		}

		private static IEnumerator LoadHatsFromDisk()
		{
			Debug.Log($"[MonAmiral] Loading hats from disk.");

			string directoryName = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			string path = System.IO.Path.Combine(directoryName, "morecustomhats");

			Debug.Log($"[MonAmiral] Path to AssetBundle: " + path);

			AssetBundleCreateRequest createRequest = AssetBundle.LoadFromMemoryAsync(System.IO.File.ReadAllBytes(path));
			yield return createRequest;
			assetBundle = createRequest.assetBundle;

			Debug.Log($"[MonAmiral] AssetBundle loaded.");

			hats = new List<HatEntry>();
			hats.Add(LoadHat("Chibidoki/chibidoki"));
			hats.Add(LoadHat("Timmy/timmyrobot"));
			hats.Add(LoadHat("Cigarette/cigarette"));
			hats.Add(LoadHat("Cigarette/cigaretteweed"));
			hats.Add(LoadHat("BuzzBall/buzzBall"));
			hats.Add(LoadHat("Gecko/gecko"));
			hats.Add(LoadHat("Fruits/applegreen"));
			hats.Add(LoadHat("Fruits/appleyellow"));
			hats.Add(LoadHat("Fruits/applered"));
			hats.Add(LoadHat("Fruits/pepperred"));
			hats.Add(LoadHat("Fruits/banana"));
			hats.Add(LoadHat("HockeyMask/hockeymask"));
			hats.Add(LoadHat("arrow/arrow"));
			hats.Add(LoadHat("DUM/dum"));
			hats.Add(LoadHat("ExtraHead/extrahead"));
			hats.Add(LoadHat("Bombzyz/reddhmis"));
			hats.Add(LoadHat("Bombzyz/yellowdhmis"));
			hats.Add(LoadHat("Bombzyz/duckdhmis"));
			hats.Add(LoadHat("Tobs/tobs"));
			hats.Add(LoadHat("Lyn/Lyn"));
			hats.Add(LoadHat("CurryPaws/CurryPaws"));
			hats.Add(LoadHat("MonAmiral/MonAmiral"));
			hats.Add(LoadHat("Deme/Deme"));
			hats.Add(LoadHat("CharborgPlush/CharborgPlush"));
			hats.Add(LoadHat("Chirpling/Chirpling"));
			hats.Add(LoadHat("PocketWaifu/FaceBunnySnout"));
			hats.Add(LoadHat("PocketWaifu/FaceMamavale"));
			hats.Add(LoadHat("PocketWaifu/FacePixelGlasses"));
			hats.Add(LoadHat("PocketWaifu/HatBear"));
			hats.Add(LoadHat("PocketWaifu/HatBeer"));
			hats.Add(LoadHat("PocketWaifu/HatBeret"));
			hats.Add(LoadHat("PocketWaifu/HatBow"));
			hats.Add(LoadHat("PocketWaifu/HatBread"));
			hats.Add(LoadHat("PocketWaifu/HatBucket"));
			hats.Add(LoadHat("PocketWaifu/HatChickEgg"));
			hats.Add(LoadHat("PocketWaifu/HatCloak"));
			hats.Add(LoadHat("PocketWaifu/HatCowboyLarge"));
			hats.Add(LoadHat("PocketWaifu/HatCowboySmall"));
			hats.Add(LoadHat("PocketWaifu/HatCrown"));
			hats.Add(LoadHat("PocketWaifu/HatFishFearMe"));
			hats.Add(LoadHat("PocketWaifu/HatFlower"));
			hats.Add(LoadHat("PocketWaifu/HatHeadphones"));
			hats.Add(LoadHat("PocketWaifu/HatPetalPal"));
			hats.Add(LoadHat("PocketWaifu/HatPetalPalBaby"));
			hats.Add(LoadHat("PocketWaifu/HatPirate"));
			hats.Add(LoadHat("PocketWaifu/HatPolice"));
			hats.Add(LoadHat("PocketWaifu/HatPombeanie"));
			hats.Add(LoadHat("PocketWaifu/HatSummerHat"));

			Debug.Log($"[MonAmiral] Done!");
		}

		private static void SetLayerRecursively(Transform transform, int layer)
		{
			transform.gameObject.layer = layer;
			foreach (Transform child in transform)
			{
				SetLayerRecursively(child, layer);
			}
		}

		private static HatEntry LoadHat(string hatName)
		{
			Debug.Log($"[MonAmiral] Loading hat '{hatName}'.");

			GameObject prefab = assetBundle.LoadAsset<GameObject>($"Assets/{hatName}.prefab");
			Texture2D icon = assetBundle.LoadAsset<Texture2D>($"Assets/{hatName}.png");

			Debug.Log($"[MonAmiral] Loaded prefab {prefab} and texture {icon}");

			return new HatEntry(hatName, prefab, icon);
		}

		public struct HatEntry
		{
			public string Name;
			public GameObject Prefab;
			public Texture2D Icon;

			public HatEntry(string name, GameObject prefab, Texture2D icon)
			{
				this.Name = name;
				this.Prefab = prefab;
				this.Icon = icon;
			}
		}
	}
}