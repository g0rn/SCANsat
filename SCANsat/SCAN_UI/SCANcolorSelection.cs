﻿#region license
/* 
 *  [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 *
 * SCANsat - Color Selection and Settings Menu
 * 
 * Copyright (c)2013 damny;
 * Copyright (c)2014 David Grandy <david.grandy@gmail.com>;
 * Copyright (c)2014 technogeeky <technogeeky@gmail.com>;
 * Copyright (c)2014 (Your Name Here) <your email here>; see LICENSE.txt for licensing details.
 *
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using SCANsat.SCAN_Data;
using SCANsat.SCAN_Map;
using SCANsat.SCAN_UI.UI_Framework;
using SCANsat.SCAN_Platform;
using SCANsat.SCAN_Platform.Palettes;
using palette = SCANsat.SCAN_UI.UI_Framework.SCANpalette;
using UnityEngine;

namespace SCANsat.SCAN_UI
{
	class SCANcolorSelection: SCAN_MBW
	{
		private bool dropDown, paletteBox, resourceBox;
		private bool oldReverseState, oldDiscreteState;
		private bool spaceCenterLock, trackingStationLock, clampState, oldClampState;
		private Rect ddRect;
		private int paletteIndex;
		private SCANmapLegend currentLegend, previewLegend;
		private int windowMode = 0;

		private SCANterrainConfig currentTerrain;
		private float minT, maxT, clampT, pSize;

		private SCANuiSlider minTerrainSlider, maxTerrainSlider, clampTerrainSlider, paletteSizeSlider, resourceMinSlider, resourceMaxSlider, resourceTransSlider, biomeTransSlider;

		private SCANuiColorPicker slopeColorPicker, biomeColorPicker, resourceColorPicker;
		private float bTrans, rTrans;

		private bool stockBiomes = false;

		private SCANresourceGlobal currentResource;
		private float lowRCutoff, highRCutoff;

		private List<SCANresourceGlobal> loadedResources;

		private bool fineControlMode, oldFineControl;
		private int bodyIndex;

		private Vector2 scrollR;
		private const string lockID = "colorLockID";
		internal static Rect defaultRect = new Rect(100, 400, 650, 360);

		//SCAN_MBW objects to sync the color selection fields to the currently displayed map
		private SCANkscMap kscMapObj;
		private SCANBigMap bigMapObj;

		private static SCANmap bigMap;
		private SCANdata data;

		protected override void Awake()
		{
			WindowCaption = "S.C.A.N. Color Management";
			WindowRect = defaultRect;
			WindowStyle = SCANskins.SCAN_window;
			WindowOptions = new GUILayoutOption[2] { GUILayout.Width(650), GUILayout.Height(360) };
			Visible = false;
			DragEnabled = true;
			ClampToScreenOffset = new RectOffset(-450, -450, -250, -250);

			SCAN_SkinsLibrary.SetCurrent("SCAN_Unity");

			InputLockManager.RemoveControlLock(lockID);
		}

		internal override void Start()
		{
			if (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
			{
				kscMapObj = (SCANkscMap)SCANcontroller.controller.kscMap;
				if (SCANkscMap.BigMap != null)
					bigMap = SCANkscMap.BigMap;
				if (kscMapObj.Data != null)
					data = kscMapObj.Data;
			}
			else if (HighLogic.LoadedSceneIsFlight)
			{
				bigMapObj = (SCANBigMap)SCANcontroller.controller.BigMap;
				if (SCANBigMap.BigMap != null)
					bigMap = SCANBigMap.BigMap;
				if (bigMapObj.Data != null)
					data = bigMapObj.Data;
			}

			if (data == null)
			{
				data = SCANUtil.getData(Planetarium.fetch.Home);
				if (data == null)
				{
					data = new SCANdata(Planetarium.fetch.Home);
					SCANcontroller.controller.addToBodyData(Planetarium.fetch.Home, data);
				}
			}

			currentTerrain = new SCANterrainConfig(data.TerrainConfig);

			stockBiomes = SCANcontroller.controller.useStockBiomes;

			minTerrainSlider = new SCANuiSlider(data.DefaultMinHeight - 10000, data.TerrainConfig.MaxTerrain - 100, data.TerrainConfig.MinTerrain, "Min: ", "m", -2);
			maxTerrainSlider = new SCANuiSlider(data.TerrainConfig.MinTerrain + 100, data.DefaultMaxHeight + 10000, data.TerrainConfig.MaxTerrain, "Max: ", "m", -2);
			clampTerrainSlider = new SCANuiSlider(data.TerrainConfig.MinTerrain + 10, data.TerrainConfig.MaxTerrain - 10, data.TerrainConfig.ClampTerrain ?? data.TerrainConfig.MinTerrain + 10, "Clamp: ", "m", -1);
			paletteSizeSlider = new SCANuiSlider(3, 12, data.TerrainConfig.PalSize, "Palette Size: ", "", 0);

			slopeColorPicker = new SCANuiColorPicker(palette.xkcd_Amber, palette.xkcd_Cerulean, true);

			biomeTransSlider = new SCANuiSlider(0, 100, SCANcontroller.controller.biomeTransparency, "Ter. Trans: ", "%", 0);

			biomeColorPicker = new SCANuiColorPicker(SCANcontroller.controller.lowBiomeColor, SCANcontroller.controller.highBiomeColor, true);

			if (SCANconfigLoader.GlobalResource)
			{
				loadedResources = SCANcontroller.setLoadedResourceList();
				currentResource = new SCANresourceGlobal(loadedResources[0]);
				currentResource.CurrentBodyConfig(data.Body.name);

				if (currentResource != null)
				{
					resourceMinSlider = new SCANuiSlider(0, currentResource.CurrentBody.MinValue - 0.1f, currentResource.CurrentBody.MinValue, "Min: ", "%", 1);
					resourceMaxSlider = new SCANuiSlider(currentResource.CurrentBody.MinValue + 0.1f, 100, currentResource.CurrentBody.MaxValue, "Max: ", "%", 1);
					resourceTransSlider = new SCANuiSlider(0, 80, currentResource.Transparency, "Trans: ", "%", 0);

					resourceColorPicker = new SCANuiColorPicker(currentResource.MinColor, currentResource.MaxColor, true);
				}
			}

			bodyIndex = data.Body.flightGlobalsIndex;

			if (windowMode > 3 || (windowMode > 2 && !SCANconfigLoader.GlobalResource))
				windowMode = 0;

			setSizeSlider(currentTerrain.ColorPal.kind);
		}

		internal override void OnDestroy()
		{
			InputLockManager.RemoveControlLock(lockID);
		}

		protected override void DrawWindowPre(int id)
		{
			//Some clumsy logic is used here to ensure that the color selection fields always remain in sync with the current map in each scene
			if (HighLogic.LoadedSceneIsFlight)
			{
				if (data == null)
				{
					data = SCANUtil.getData(FlightGlobals.currentMainBody);
					if (data == null)
					{
						data = new SCANdata(FlightGlobals.currentMainBody);
						SCANcontroller.controller.addToBodyData(FlightGlobals.currentMainBody, data);
					}
				}

				if (bigMapObj.Visible && SCANBigMap.BigMap != null)
				{
					data = bigMapObj.Data;
					bigMap = SCANBigMap.BigMap;
				}
				else if (data.Body != FlightGlobals.currentMainBody)
				{
					data = SCANUtil.getData(FlightGlobals.currentMainBody);
					if (data == null)
					{
						data = new SCANdata(FlightGlobals.currentMainBody);
						SCANcontroller.controller.addToBodyData(FlightGlobals.currentMainBody, data);
					}
				}

				if (bigMap == null)
				{
					if (SCANBigMap.BigMap != null)
					{
						bigMap = SCANBigMap.BigMap;
					}
				}
			}

			//Lock space center click through - Sync SCANdata
			else if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
			{
				if (data == null)
				{
					data = SCANUtil.getData(Planetarium.fetch.Home);
					if (data == null)
					{
						data = new SCANdata(Planetarium.fetch.Home);
						SCANcontroller.controller.addToBodyData(Planetarium.fetch.Home, data);
					}
				}
				if (kscMapObj.Visible)
				{
					data = kscMapObj.Data;
					bigMap = SCANkscMap.BigMap;
				}
				else if (data.Body != Planetarium.fetch.Home)
				{
					data = SCANUtil.getData(Planetarium.fetch.Home);
					if (data == null)
					{
						data = new SCANdata(Planetarium.fetch.Home);
						SCANcontroller.controller.addToBodyData(Planetarium.fetch.Home, data);
					}
				}
				if (bigMap == null)
				{
					if (SCANkscMap.BigMap != null)
					{
						bigMap = SCANkscMap.BigMap;
					}
				}
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y;
				if (WindowRect.Contains(mousePos) && !spaceCenterLock)
				{
					InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.KSC_ALL, lockID);
					spaceCenterLock = true;
				}
				else if (!WindowRect.Contains(mousePos) && spaceCenterLock)
				{
					InputLockManager.RemoveControlLock(lockID);
					spaceCenterLock = false;
				}
			}

			//Lock tracking scene click through - Sync SCANdata
			else if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
			{
				if (data == null)
				{
					data = SCANUtil.getData(Planetarium.fetch.Home);
					if (data == null)
					{
						data = new SCANdata(Planetarium.fetch.Home);
						SCANcontroller.controller.addToBodyData(Planetarium.fetch.Home, data);
					}
				}
				if (kscMapObj.Visible)
				{
					data = kscMapObj.Data;
					bigMap = SCANkscMap.BigMap;
				}
				else if (data.Body != Planetarium.fetch.Home)
				{
					data = SCANUtil.getData(Planetarium.fetch.Home);
					if (data == null)
					{
						data = new SCANdata(Planetarium.fetch.Home);
						SCANcontroller.controller.addToBodyData(Planetarium.fetch.Home, data);
					}
				}
				if (bigMap == null)
				{
					if (SCANkscMap.BigMap != null)
					{
						bigMap = SCANkscMap.BigMap;
					}
				}
				Vector2 mousePos = Input.mousePosition;
				mousePos.y = Screen.height - mousePos.y;
				if (WindowRect.Contains(mousePos) && !trackingStationLock)
				{
					InputLockManager.SetControlLock(ControlTypes.TRACKINGSTATION_UI, lockID);
					trackingStationLock = true;
				}
				else if (!WindowRect.Contains(mousePos) && trackingStationLock)
				{
					InputLockManager.RemoveControlLock(lockID);
					trackingStationLock = false;
				}
			}

			//This updates all of the fields whenever the palette selection is changed
			if (windowMode == 0 && (currentLegend == null || bodyIndex != data.Body.flightGlobalsIndex))// || currentTerrain.ColorPal.hash != data.TerrainConfig.ColorPal.hash))
			{
				currentTerrain = new SCANterrainConfig(data.TerrainConfig);

				SCANUtil.SCANdebugLog("Trigger Body Change");
				bodyIndex = data.Body.flightGlobalsIndex;

				currentTerrain = new SCANterrainConfig(data.TerrainConfig);

				updateUI();
			}

			if (windowMode == 0 && previewLegend == null)
			{
				drawPreviewLegend();
			}

			if (!dropDown)
			{
				paletteBox = false;
				resourceBox = false;
			}
		}

		protected override void DrawWindow(int id)
		{
			versionLabel(id);					/* The standard version number and close button */
			closeBox(id);

			growS();
				windowTabs(id);					/* Draws the window selection tabs across the top */
				if (windowMode == 0)
				{
					growE();
					paletteTextures(id);		/* Draws the palette selection button and preview swatches */
					paletteOptions(id);			/* All of the terrain and palette options */
					stopE();
					fillS(8);
					growE();
					palettePreview(id);			/* Draws the two preview palette legends */
					fillS(10);
					paletteConfirmation(id);	/* The buttons for default, apply, and cancel */
					stopE();
				}
				else if (windowMode == 1)
				{
					growE();
						slopeColorPicker.drawColorSelector(WindowRect);
					stopE();
				}
				else if (windowMode == 2)
				{
					growE();
						fillS(20);
						biomeColorPicker.drawColorSelector(WindowRect);
						fillS(70);
						growS();
							biomeOptions(id);
							biomeConfirm(id);
						stopS();
					stopE();
				}
				else if (windowMode == 3 && SCANconfigLoader.GlobalResource)
				{
					growE();
						fillS(20);
						resourceColorPicker.drawColorSelector(WindowRect);
						fillS(70);
						growS();
							resourceOptions(id);
							resourceConfirm(id);
						stopS();
					stopE();
				}
				else
					windowMode = 0;
			stopS();

			dropDownBox(id);				/* Draw the drop down menu for the palette selection box */
		}

		protected override void DrawWindowPost(int id)
		{
			if (dropDown && Event.current.type == EventType.mouseDown && !ddRect.Contains(Event.current.mousePosition))
			{
				dropDown = false;
			}

			//These methods update all of the UI elements whenever any of the options are changed
			if (windowMode == 0)
			{
				if (currentTerrain.PalRev != oldReverseState)
				{
					oldReverseState = currentTerrain.PalRev;
					drawPreviewLegend();
				}

				if (minTerrainSlider.valueChanged() || maxTerrainSlider.valueChanged())
				{
					setTerrainSliders();
				}

				if (currentTerrain.PalDis != oldDiscreteState)
				{
					oldDiscreteState = currentTerrain.PalDis;
					drawPreviewLegend();
				}

				if (clampState != oldClampState)
				{
					oldClampState = clampState;
					drawPreviewLegend();
				}

				if (paletteSizeSlider.valueChanged())
				{
					regenPaletteSets();
					currentTerrain.ColorPal = palette.CurrentPalettes.availablePalettes[paletteIndex];
					drawPreviewLegend();
				}
			}
			else if (windowMode == 1)
			{
				slopeColorPicker.colorStateChanged();
				slopeColorPicker.brightnessChanged();
			}
			else if (windowMode == 2)
			{
				biomeColorPicker.colorStateChanged();
				biomeColorPicker.brightnessChanged();
			}
			else if (windowMode == 3)
			{
				if (resourceMinSlider.valueChanged() || resourceMaxSlider.valueChanged())
				{
					setResourceSliders();
				}

				if (bodyIndex != data.Body.flightGlobalsIndex)
				{
					SCANUtil.SCANdebugLog("Trigger Body Change");
					bodyIndex = data.Body.flightGlobalsIndex;

					currentResource.CurrentBodyConfig(data.Body.name);

					lowRCutoff = currentResource.CurrentBody.MinValue;
					highRCutoff = currentResource.CurrentBody.MaxValue;

					oldFineControl = fineControlMode = false;

					setResourceSliders();
				}

				if (oldFineControl != fineControlMode)
				{
					oldFineControl = fineControlMode;
					if (fineControlMode)
					{
						if (lowRCutoff < 5f)
							resourceMinSlider.MinValue = 0f;
						else
							resourceMinSlider.MinValue = lowRCutoff - 5;

						if (lowRCutoff > 95f)
							resourceMinSlider.MaxValue = 100f;
						else if (highRCutoff < lowRCutoff + 5f)
							resourceMinSlider.MaxValue = highRCutoff - 0.1f;
						else
							resourceMinSlider.MaxValue = lowRCutoff + 5f;

						if (highRCutoff < 5f)
							resourceMaxSlider.MinValue = 0f;
						else if (lowRCutoff > highRCutoff - 5f)
							resourceMaxSlider.MinValue = lowRCutoff + 0.1f;
						else
							resourceMaxSlider.MinValue = highRCutoff - 5f;

						if (highRCutoff > 95f)
							resourceMaxSlider.MaxValue = 100f;
						else
							resourceMaxSlider.MaxValue = highRCutoff + 5f;
					}
					else
						setResourceSliders();
				}

				resourceColorPicker.colorStateChanged();
				resourceColorPicker.brightnessChanged();
			}
		}

		//Draw the version label in the upper left corner
		private void versionLabel(int id)
		{
			Rect r = new Rect(6, 0, 50, 18);
			GUI.Label(r, SCANmainMenuLoader.SCANsatVersion, SCANskins.SCAN_whiteReadoutLabel);
		}

		//Draw the close button in the upper right corner
		private void closeBox(int id)
		{
			Rect r = new Rect(WindowRect.width - 20, 0, 18, 18);
			if (GUI.Button(r, SCANcontroller.controller.closeBox, SCANskins.SCAN_closeButton))
			{
				InputLockManager.RemoveControlLock(lockID);
				spaceCenterLock = false;
				trackingStationLock = false;
				Visible = false;
			}
		}

		//Draw the window tab options
		private void windowTabs(int id)
		{
			growE();
				if (GUILayout.Button("Altimetry"))
				{
					windowMode = 0;

					currentTerrain = new SCANterrainConfig(data.TerrainConfig);

					bodyIndex = data.Body.flightGlobalsIndex;

					updateUI();
				}
				if (GUILayout.Button("Slope"))
				{
					windowMode = 1;
				}
				if (GUILayout.Button("Biome"))
				{
					windowMode = 2;

					fineControlMode = oldFineControl = false;
				}
				if (SCANconfigLoader.GlobalResource)
				{
					if (GUILayout.Button("Resources"))
					{
						windowMode = 3;

						fineControlMode = oldFineControl = false;

						currentResource.CurrentBodyConfig(data.Body.name);

						bodyIndex = data.Body.flightGlobalsIndex;

						updateUI();
					}
				}
			stopE();
		}

		//Draw the palette selection field
		private void paletteTextures(int id)
		{
			growS();
				GUILayout.Label("Palette Selection", SCANskins.SCAN_headline);
				fillS(12);
				growE();
					if (GUILayout.Button("Palette Style:", SCANskins.SCAN_buttonFixed, GUILayout.MaxWidth(120)))
					{
						dropDown = !dropDown;
						paletteBox = !paletteBox;
					}
					fillS(10);
					GUILayout.Label(palette.getPaletteTypeName, SCANskins.SCAN_whiteReadoutLabel);
				stopE();
				growE();
					// This integer stores the amount of palettes of each type
					int j = 9;
					if (palette.CurrentPalettes.paletteType == Palette.Kind.Sequential)
						j = 12;
					else if (palette.CurrentPalettes.paletteType == Palette.Kind.Qualitative)
						j = 8;
					else if (palette.CurrentPalettes.paletteType == Palette.Kind.Invertable || palette.CurrentPalettes.paletteType == Palette.Kind.Unknown)
						j = 0;
					for (int i = 0; i < j; i++)
					{
						if (i % 3 == 0)
						{
							stopE();
							fillS(9);
							growE();
						}
						Texture2D t = palette.CurrentPalettes.paletteSwatch[i];
						if (paletteBox)
						{
							GUILayout.Label("", GUILayout.Width(110), GUILayout.Height(25));
						}
						else
						{
							if (GUILayout.Button("", SCANskins.SCAN_texButton, GUILayout.Width(110), GUILayout.Height(25)))
							{
								currentTerrain.ColorPal = palette.CurrentPalettes.availablePalettes[i];
								paletteIndex = currentTerrain.ColorPal.index;
								updateUI();
								drawPreviewLegend();
							}
						}
						Rect r = GUILayoutUtility.GetLastRect();
						r.width -= 10;
						GUI.DrawTexture(r, t);
					}
				stopE();
			stopS();
		}

		//Main palette option settings
		private void paletteOptions(int id)
		{
			growS();
				fillS(4);
				GUILayout.Label("Terrain Options: " + data.Body.name, SCANskins.SCAN_headlineSmall);

				growE();
					fillS(10);
					currentTerrain.MinTerrain = minTerrainSlider.drawSlider(false, ref minT);
				stopE();
				fillS(8);
				growE();
					fillS(10);
					currentTerrain.MaxTerrain = maxTerrainSlider.drawSlider(false, ref maxT);
				stopE();
				fillS(6);
				growE();
					fillS();
					clampState = GUILayout.Toggle(clampState, "Clamp Terrain", SCANskins.SCAN_settingsToggle, GUILayout.Width(100));
					fillS();
				stopE();
				if (clampState)
					{
						growE();
							fillS(10);
							currentTerrain.ClampTerrain = clampTerrainSlider.drawSlider(false, ref clampT);
						stopE();
					}
				fillS(6);
				GUILayout.Label("Palette Options", SCANskins.SCAN_headlineSmall);
				if (palette.CurrentPalettes.paletteType != Palette.Kind.Fixed)
				{
					growE();
						fillS(10);
						currentTerrain.PalSize = (int)paletteSizeSlider.drawSlider(false, ref pSize);
					stopE();
				}

				growE();
					currentTerrain.PalRev = GUILayout.Toggle(currentTerrain.PalRev, "Reverse Order", SCANskins.SCAN_settingsToggle);
					fillS(10);
					currentTerrain.PalDis = GUILayout.Toggle(currentTerrain.PalDis, "Discrete Gradient", SCANskins.SCAN_settingsToggle);
				stopE();

			stopS();
		}

		//Two boxes to show the current and new palettes as they appear on the legend
		private void palettePreview(int id)
		{
			growS();
				GUILayout.Label("Current Palette", SCANskins.SCAN_headlineSmall);
				fillS(8);
				GUILayout.Label("", SCANskins.SCAN_legendTex, GUILayout.Width(180), GUILayout.Height(25));
				Rect r = GUILayoutUtility.GetLastRect();
				GUI.DrawTexture(r, currentLegend.Legend);
			stopS();
			fillS(8);
			growS();
				GUILayout.Label("New Palette", SCANskins.SCAN_headlineSmall);
				fillS(8);
				GUILayout.Label("", SCANskins.SCAN_legendTex, GUILayout.Width(180), GUILayout.Height(25));
				r = GUILayoutUtility.GetLastRect();
				GUI.DrawTexture(r, previewLegend.Legend);
			stopS();
		}

		//Buttons to apply the new palette or cancel and return to the original
		private void paletteConfirmation(int id)
		{
			growS();
				fillS(6);
				if (GUILayout.Button("Default Values", GUILayout.Width(135)))
				{
					currentTerrain.MinTerrain = data.DefaultMinHeight;
					currentTerrain.MaxTerrain = data.DefaultMaxHeight;
					currentTerrain.ClampTerrain = data.DefaultClampHeight;
					currentTerrain.ColorPal = data.DefaultColorPalette;
					currentTerrain.PalRev = data.DefaultReversePalette;
					currentTerrain.PalDis = false;
					currentTerrain.PalSize = data.DefaultColorPalette.size;
					
					updateUI();

					if (bigMap != null)
						bigMap.resetMap();
				}
				fillS(6);
				growE();
					if (GUILayout.Button("Apply Values", GUILayout.Width(60)))
					{
						SCANcontroller.updateTerrainConfig(currentTerrain);

						updateUI();

						if (bigMap != null)
							bigMap.resetMap();
					}
					fillS(10);
					if (GUILayout.Button("Cancel", GUILayout.Width(60)))
					{
						InputLockManager.RemoveControlLock(lockID);
						spaceCenterLock = false;
						trackingStationLock = false;
						Visible = false;
					}
				stopE();
			stopS();
		}

		private void biomeOptions(int id)
		{
			GUILayout.Label("Biome Options", SCANskins.SCAN_headline, GUILayout.Width(300));

			fillS(20);
			stockBiomes = GUILayout.Toggle(stockBiomes, "Use Stock Biome Maps", SCANskins.SCAN_toggle);
			fillS(8);
			growE();
				fillS(10);
				biomeTransSlider.drawSlider(false, ref bTrans);
			stopE();
		}

		private void resourceOptions(int id)
		{
			GUILayout.Label("Resource Options: " + data.Body.name, SCANskins.SCAN_headline, GUILayout.Width(300));

			fillS(10);
			growE();
				if (GUILayout.Button("Resource Selection", SCANskins.SCAN_buttonFixed))
				{
					dropDown = !dropDown;
					resourceBox = !resourceBox;
				}
				fillS(10);
				GUILayout.Label(currentResource.Name, SCANskins.SCAN_whiteReadoutLabel);
			stopE();
			fillS(20);
			growE();
				fillS(110);
				fineControlMode = GUILayout.Toggle(fineControlMode, "Fine Control Mode", SCANskins.SCAN_settingsToggle);
			stopE();
			growE();
				fillS(10);
				currentResource.CurrentBody.MinValue = resourceMinSlider.drawSlider(dropDown, ref lowRCutoff);
			stopE();
			fillS(8);
			growE();
				fillS(10);
				currentResource.CurrentBody.MaxValue = resourceMaxSlider.drawSlider(dropDown, ref highRCutoff);
			stopE();
			fillS(8);
			growE();
				fillS(10);
				currentResource.Transparency = resourceTransSlider.drawSlider(dropDown, ref rTrans);
			stopE();
		}

		private void biomeConfirm(int id)
		{
			fillS(10);
			if (GUILayout.Button("Default Values", GUILayout.Width(135)))
			{
				SCANcontroller.controller.lowBiomeColor = SCANcontroller.controller.defaultLowBiomeColor;
				SCANcontroller.controller.highBiomeColor = SCANcontroller.controller.defaultHighBiomeColor;
				SCANcontroller.controller.useStockBiomes = false;
				SCANcontroller.controller.biomeTransparency = 40f;

				stockBiomes = false;

				biomeColorPicker = new SCANuiColorPicker(SCANcontroller.controller.lowBiomeColor, SCANcontroller.controller.highBiomeColor, biomeColorPicker.LowColorChange);

				biomeColorPicker.updateOldSwatches();

				bTrans = SCANcontroller.controller.biomeTransparency;

				if (bigMap != null)
					bigMap.resetMap();
			}
			fillS(6);
			growE();
			if (GUILayout.Button("Apply Values", GUILayout.Width(60)))
			{
				SCANcontroller.controller.lowBiomeColor = biomeColorPicker.ColorLow;
				SCANcontroller.controller.highBiomeColor = biomeColorPicker.ColorHigh;
				SCANcontroller.controller.useStockBiomes = stockBiomes;
				SCANcontroller.controller.biomeTransparency = bTrans;

				biomeColorPicker.updateOldSwatches();

				if (bigMap != null)
					bigMap.resetMap();
			}
			fillS(10);
			if (GUILayout.Button("Cancel", GUILayout.Width(60)))
			{
				InputLockManager.RemoveControlLock(lockID);
				spaceCenterLock = false;
				trackingStationLock = false;
				Visible = false;
			}
			stopE();
		}

		private void resourceConfirm(int id)
		{
			fillS(10);
			growE();
			if (GUILayout.Button("Apply Values", GUILayout.Width(100)))
			{
				currentResource.MinColor = resourceColorPicker.ColorLow;
				currentResource.MaxColor = resourceColorPicker.ColorHigh;

				SCANcontroller.updateSCANresource(currentResource, false);

				updateUI();

				if (bigMap != null)
					bigMap.resetMap();
			}

				fillS(6);

				if (GUILayout.Button("Apply To All Planets", GUILayout.Width(120)))
				{
					for (int i = 0; i < currentResource.getBodyCount; i++)
					{
						SCANresourceBody r = currentResource.getBodyConfig(i);
						if (r != null)
						{
							r.MinValue = lowRCutoff;
							r.MaxValue = highRCutoff;
						}
					}

					currentResource.MinColor = resourceColorPicker.ColorLow;
					currentResource.MaxColor = resourceColorPicker.ColorHigh;

					SCANcontroller.updateSCANresource(currentResource, true);

					updateUI();

					if (bigMap != null)
						bigMap.resetMap();
				}
			stopE();
			fillS(8);
			growE();
				if (GUILayout.Button("Default Values", GUILayout.Width(110)))
				{
					currentResource.CurrentBody.MinValue = currentResource.CurrentBody.DefaultMinValue;
					currentResource.CurrentBody.MaxValue = currentResource.CurrentBody.DefaultMaxValue;
					currentResource.MinColor = currentResource.ResourceType.ColorEmpty;
					currentResource.MaxColor = currentResource.ResourceType.ColorFull;
					currentResource.Transparency = 20f;

					SCANcontroller.updateSCANresource(currentResource, false);

					updateUI();

					if (bigMap != null)
						bigMap.resetMap();
				}

				fillS(6);

				if (GUILayout.Button("Default Values For All Planets", GUILayout.Width(140)))
				{
					currentResource.MinColor = currentResource.ResourceType.ColorEmpty;
					currentResource.MaxColor = currentResource.ResourceType.ColorFull;
					currentResource.Transparency = 20f;

					for (int i = 0; i < currentResource.getBodyCount; i++)
					{
						SCANresourceBody r = currentResource.getBodyConfig(i);
						if (r != null)
						{
							r.MinValue = r.DefaultMinValue;
							r.MaxValue = r.DefaultMaxValue;
						}
					}

					SCANcontroller.updateSCANresource(currentResource, true);

					updateUI();

					if (bigMap != null)
						bigMap.resetMap();
				}
			stopE();
		}

		//Drop down menu for palette selection
		private void dropDownBox(int id)
		{
			if (dropDown)
			{
				if (paletteBox && windowMode == 0)
				{
					ddRect = new Rect(40, 120, 100, 100);
					GUI.Box(ddRect, "", SCANskins.SCAN_dropDownBox);
					for (int i = 0; i < Palette.kindNames.Length; i++)
					{
						Rect r = new Rect(ddRect.x + 10, ddRect.y + 5 + (i * 23), 80, 22);
						if (GUI.Button(r, Palette.kindNames[i], SCANskins.SCAN_dropDownButton))
						{
							paletteBox = false;
							palette.CurrentPalettes = palette.setCurrentPalettesType((Palette.Kind)i, (int)pSize);
							setSizeSlider((Palette.Kind)i);
						}
					}
				}
				else if (resourceBox && windowMode == 3)
				{
					ddRect = new Rect(WindowRect.width - 320, 115, 160, 140);
					GUI.Box(ddRect, "", SCANskins.SCAN_dropDownBox);
					for (int i = 0; i < loadedResources.Count; i ++)
					{
						scrollR = GUI.BeginScrollView(ddRect, scrollR, new Rect(0, 0, 140, 23 * loadedResources.Count));
						Rect r = new Rect(2, i * 23, 136, 22);
						if (GUI.Button(r, loadedResources[i].Name, SCANskins.SCAN_dropDownButton))
						{
							currentResource = new SCANresourceGlobal(loadedResources[i]);
							currentResource.CurrentBodyConfig(data.Body.name);

							fineControlMode = oldFineControl = false;

							updateUI();

							dropDown = false;
							resourceBox = false;
						}
						GUI.EndScrollView();
					}
				}
				else
					dropDown = false;
			}
		}

		private void updateUI()
		{
			if (windowMode == 0)
			{
				minT = currentTerrain.MinTerrain;
				maxT = currentTerrain.MaxTerrain;
				clampT = currentTerrain.ClampTerrain ?? currentTerrain.MinTerrain + 10f;
				pSize = currentTerrain.PalSize;
				oldReverseState = currentTerrain.PalRev;
				oldDiscreteState = currentTerrain.PalDis;
				oldClampState = clampState = currentTerrain.ClampTerrain != null;
				palette.CurrentPalettes = palette.setCurrentPalettesType(currentTerrain.ColorPal.kind, (int)pSize);
				minTerrainSlider.valueChanged();
				maxTerrainSlider.valueChanged();
				clampTerrainSlider.valueChanged();
				paletteSizeSlider.valueChanged();
				paletteIndex = currentTerrain.ColorPal.index;

				regenPaletteSets();
				setSizeSlider(currentTerrain.ColorPal.kind);
				setTerrainSliders();
				drawCurrentLegend();
			}
			else if (windowMode == 3)
			{
				lowRCutoff = currentResource.CurrentBody.MinValue;
				highRCutoff = currentResource.CurrentBody.MaxValue;
				rTrans = currentResource.Transparency;

				resourceColorPicker = new SCANuiColorPicker(currentResource.MinColor, currentResource.MaxColor, resourceColorPicker.LowColorChange);

				resourceColorPicker.updateOldSwatches();

				setResourceSliders();
			}
		}

		//Draws the palette swatch for the currently active SCANdata selection
		private void drawCurrentLegend()
		{
			currentLegend = new SCANmapLegend();
			currentLegend.Legend = currentLegend.getLegend(0, data);
			//currentLegend = SCANmapLegend.getLegend(0, data);
		}

		//Draws the palette swatch for the newly adjusted palette
		private void drawPreviewLegend()
		{
			float? clamp = null;
			Color32[] c = currentTerrain.ColorPal.colors;
			if (clampState)
				clamp = clampT;
			if (currentTerrain.PalRev)
				c = currentTerrain.ColorPal.colorsReverse;
			previewLegend = new SCANmapLegend();
			previewLegend.Legend = previewLegend.getLegend(maxT, minT, clamp, currentTerrain.PalDis, c);
		}

		//Resets the palettes whenever the size slider is adjusted
		private void regenPaletteSets()
		{
			//palette.DivPaletteSet = palette.generatePaletteSet((int)paletteSizeSlider.CurrentValue, Palette.Kind.Diverging);
			//palette.QualPaletteSet = palette.generatePaletteSet((int)paletteSizeSlider.CurrentValue, Palette.Kind.Qualitative);
			//palette.SeqPaletteSet = palette.generatePaletteSet((int)paletteSizeSlider.CurrentValue, Palette.Kind.Sequential);
			//palette.FixedPaletteSet = palette.generatePaletteSet(0, Palette.Kind.Fixed);
			palette.CurrentPalettes = palette.setCurrentPalettesType(palette.CurrentPalettes.paletteType, (int)pSize/*(int)paletteSizeSlider.CurrentValue*/);
				//palette.setCurrentPalettesType(palette.CurrentPalettes.paletteType);
		}

		//Change the max range on the palette size slider based on palette type
		private void setSizeSlider(Palette.Kind k)
		{
			int max = 11;
			switch (k)
			{
				case Palette.Kind.Diverging:
					{
						max = 11;
						break;
					}
				case Palette.Kind.Qualitative:
					{
						max = 12;
						break;
					}
				case Palette.Kind.Sequential:
					{
						max = 9;
						break;
					}
				case Palette.Kind.Fixed:
					{
						break;
					}
			}

			paletteSizeSlider.MaxValue = max;
			if (pSize > paletteSizeSlider.MaxValue)
				pSize = paletteSizeSlider.MaxValue;
			
		}

		//Dynamically adjust the min and max values on all of the terrain height sliders; avoids impossible values
		private void setTerrainSliders()
		{
			minTerrainSlider.MinValue = data.DefaultMinHeight - 10000f;
			maxTerrainSlider.MaxValue = data.DefaultMaxHeight + 10000f;
			minTerrainSlider.MaxValue = maxT - 100f;
			maxTerrainSlider.MinValue = minT + 100f;
			clampTerrainSlider.MinValue = minT + 10f;
			clampTerrainSlider.MaxValue = maxT - 10f;
			if (clampT < minT + 10f)
				clampT = minT + 10f;
			else if (clampT > maxT - 10f)
				clampT = maxT - 10f;
		}

		private void setResourceSliders()
		{
			if (fineControlMode)
			{
				if (highRCutoff < lowRCutoff + 5f)
					resourceMinSlider.MaxValue = highRCutoff - 0.1f;

				if (lowRCutoff > highRCutoff - 5f)
					resourceMaxSlider.MinValue = lowRCutoff + 0.1f;
			}
			else
			{
				resourceMinSlider.MinValue = 0f;
				resourceMinSlider.MaxValue = highRCutoff - 0.1f;
				resourceMaxSlider.MinValue = lowRCutoff + 0.1f;
				resourceMaxSlider.MaxValue = 100f;
			}
		}

	}
}
