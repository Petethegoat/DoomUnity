﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityMidi;
using AudioSynthesis.Midi;
using AudioSynthesis.Bank;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class IwadInfo {
	public string name;
	public string[] filenames;
	public string mapnameFormat;
	public string titleMusic;
}

[System.Serializable]
public class IwadData {
	public IwadInfo[] iwads;
}

public class CommandlineArguments {
	public List<string> pwads;
	public string iwad;
	public string warp;
	public string soundfont;

	public CommandlineArguments() {
		pwads = new List<string>();
		iwad = "";
		warp = "";
		soundfont = "";
	}
}

/*

The "Master" controller. Does most things

This handles the following:
Loading the engine wad ("nasty.wad")
Loading the iwads
Creating the map and player
Cheats
Switching to new wads
Activating and using the menu
More stuff

*/

public class GameSetup : MonoBehaviour {

	public GameObject playerPrefab;
	private GameObject player;
	private string currentMap = "";
	private MapBuilder mapBuilder;
	public static WadFile wad;
	private DoomMenu menu;
	private bool menuActive;
	private TitleSetup title;
	private enum MapFormat {
		MAP,
		EM
	}
	private MapFormat mapFormat;
	private WadFile engineWad;

	private bool iwadSelector = false;
	private List<IwadInfo> foundIwads;

	private MidiPlayer midiPlayer;
	private Dictionary<string,MapInfo> mapinfo;
	public bool midiEnabled = false;
	[SerializeField] private string editorArgs;

	private CommandlineArguments args;

	// Use this for initialization
	void Start () {

		ParseArguments();

		if (args.soundfont == "") midiEnabled = false;

		if (midiEnabled) {
			if (File.Exists(args.soundfont)) {
				midiPlayer = gameObject.AddComponent<MidiPlayer>();
				midiPlayer.LoadBank(new PatchBank(File.OpenRead(args.soundfont)));
			} else {
				Debug.LogError("No soundfont found, disabling midi");
			}
		}

		cheatCodes = new List<string>() {
			"idclev",
			"kill",
			"test"
		};

		engineWad = new WadFile("nasty.wad");
		SetupTitleCamera();
		mapinfo = MapInfoLump.Load(engineWad.GetLumpAsText("NMAPINFO"));

		
		IwadData iwadData = JsonUtility.FromJson<IwadData>(engineWad.GetLumpAsText("IWADS"));
		

		if (args.iwad == "") { // Run IWAD selection tool

			foundIwads = new List<IwadInfo>();
			for (int i = 0; i < iwadData.iwads.Length; i++) {
				for (int j = 0; j < iwadData.iwads[i].filenames.Length; j++) {
					if (System.IO.File.Exists(iwadData.iwads[i].filenames[j])) {
						foundIwads.Add(iwadData.iwads[i]);
					}
				}
			}

			if (foundIwads.Count == 0) {
				Debug.LogError("Cannot find any iwads!");
			}
			if (foundIwads.Count > 1) {
				iwadSelector = true;
			}

			if (foundIwads.Count == 1) {
				SetupWad(foundIwads[0]);
			}

		} else {
			for (int i = 0; i < iwadData.iwads.Length; i++) {
				if (args.iwad == iwadData.iwads[i].filenames[0]) {
					SetupWad(iwadData.iwads[i]);
					break;
				}
			}
		}


		
	}

	void SetupTitleCamera() {
		GameObject titleCameraObject = new GameObject("TitleCamera");
		Camera titleCamera = titleCameraObject.AddComponent<Camera>();
		titleCamera.orthographic = true;
		titleCamera.orthographicSize = 1f;
		GameObject titleQuad = new GameObject("TitleQuad");
		titleQuad.transform.parent = titleCameraObject.transform;
		titleQuad.transform.localPosition = new Vector3(0f, 0f, 1f);
		titleQuad.transform.localScale = new Vector3(3.2f, -2f, 1f);
		title = titleQuad.AddComponent<TitleSetup>();
		title.Build(engineWad);
	}

	void SetupWad(IwadInfo info) {
		wad = new WadFile(info.filenames[0]);
		if (info.mapnameFormat == "MAP") mapFormat = MapFormat.MAP;
		if (info.mapnameFormat == "EM") mapFormat = MapFormat.EM;
		iwadSelector = false;

		for (int i = 0; i < args.pwads.Count; i++) {
			wad.Merge(args.pwads[i]);
		}

		StartGame(info);
	}

	void OnGUI() {
		if (iwadSelector) {
			for (int i = 0; i < foundIwads.Count; i++) {
				if (GUI.Button(new Rect(10, 10 + (i * 25), 200, 20), foundIwads[i].name)) {
					SetupWad(foundIwads[i]);
				}
			}
		}
	}

	void ParseArguments() {
		string[] arguments = System.Environment.GetCommandLineArgs();
		#if UNITY_EDITOR
		arguments = editorArgs.Split(' ');
		#endif
		args = new CommandlineArguments();

		for (int i = 0; i < arguments.Length; i++) {
			if (arguments[i] == "-iwad") {
				args.iwad = arguments[i+1];
			}

			if (arguments[i] == "-file") {
				int j = i + 1;
				while (j < arguments.Length && arguments[j][0] != '-') {
					args.pwads.Add(arguments[j]);
					j++;
				}
			}

			if (arguments[i] == "-warp") {
				args.warp = arguments[i+1];
			}

			if (arguments[i] == "-soundfont") {
				args.soundfont = arguments[i+1];
			}
		}
	}

	void StartGame(IwadInfo info) {
		mapBuilder = new MapBuilder();

		if (args.warp == "") {
			title.Build(wad);
			PlayMidi(info.titleMusic);
		} else {
			title.DisableCamera();
			menuActive = false;
			BuildMap(args.warp);
		}
		menu = new DoomMenu(wad);
	}

	void PlayMidi(string name) {
		if (midiPlayer != null) {
			midiPlayer.Stop();
			MidiFile midi = new MidiFile(wad.GetLump(name));
			midiPlayer.LoadMidi(midi);
			midiPlayer.Play();
		}
	}

	void BuildMap(string mapname) {
		if (GameObject.Find(currentMap) != null) GameObject.Destroy(GameObject.Find(currentMap));
		float time = Time.realtimeSinceStartup;
		currentMap = mapname;
		mapBuilder.BuildMap(wad, mapname);
		Debug.Log("Map build time: "+(Time.realtimeSinceStartup-time));
		CreatePlayer();
		PlayMidi(mapinfo[currentMap].music);
	}

	void CreatePlayer() {
		int playerIndex = mapBuilder.GetIndexOfThing(1);
		Thing playerThing = mapBuilder.map.things[playerIndex];
		float playerScale = 0.6f;

		if (player != null) {
			GameObject.Destroy(player);
		}

		player = Instantiate(playerPrefab);
		player.transform.localScale = new Vector3(playerScale,playerScale,playerScale);
		player.transform.position = new Vector3(playerThing.x * mapBuilder.SCALE, 
								 			    (mapBuilder.thingSectors[playerIndex].floorHeight + mapBuilder.PLAYER_HEIGHT) * mapBuilder.SCALE, 
								 			    playerThing.y * mapBuilder.SCALE);
		player.transform.localEulerAngles = new Vector3(0f, 90f - playerThing.angle, 0f);
	}
	
	private List<string> cheatCodes;
	private bool cheatLevelChange = false;
	public string levelChangeId = "";
	public string currentCheat = "";

	// Update is called once per frame
	void Update () {
		
		MenuUpdate();
		CheatUpdate();

	}

	string GetMapName(int index) {
		string output;
		
		if (mapFormat == MapFormat.MAP) {
			output = index.ToString();
			if (output.Length == 1) output = "0"+output;
			return "MAP"+output;
		}

		if (mapFormat == MapFormat.EM) {
			string indexS = index.ToString();
			if (indexS.Length == 1) indexS = "0"+indexS;
			output = "E";
			output += indexS[0];
			output += "M";
			output += indexS[1];
			return output;
		}
		return "";
	}

	void MenuUpdate() {

		if (Input.GetKeyDown(KeyCode.Escape)) {
			menuActive = !menuActive;
			SetPlayerActive(!menuActive);
			title.Darken(menuActive);
			menu.Show(menuActive);
		}

		if (menuActive) {
			menu.Update(Time.deltaTime);

			if (Input.GetKeyDown(KeyCode.DownArrow)) {
				menu.Down();
			}

			if (Input.GetKeyDown(KeyCode.UpArrow)) {
				menu.Up();
			}

			if (Input.GetKeyDown(KeyCode.Return)) {
				int item = menu.Accept();
				if (item == 0) {
					title.DisableCamera();
					menu.Show(false, true);
					menuActive = false;
					BuildMap(GetMapName((mapFormat==MapFormat.MAP)?1:11));
				}

				if (item == 4) {
					#if UNITY_EDITOR
						EditorApplication.isPlaying = false;
					#endif
					Application.Quit();
				}
			}
		}

	}

	void SetPlayerActive(bool active) {
		if (player != null) {
			player.GetComponent<MouseLook>().enabled = active;
			player.GetComponent<FirstPersonDrifter>().enabled = active;
		}	
	}

	void CheatUpdate() {
		// Cheats here

		foreach(char c in Input.inputString) {
			if (cheatLevelChange != true) {
				bool found = false;
				foreach(string s in cheatCodes) {
					if (s[currentCheat.Length] == c) {
						found = true;
						break;
					}
				}
				if (found) {
					currentCheat += c;
				} else {
					//Debug.Log(currentCheat + c);
					currentCheat = "";
				}
			} else {
				if (!Char.IsDigit(c)) {
					cheatLevelChange = false;
					currentCheat = "";
					levelChangeId = "";
				} else {
					levelChangeId += c;
				}

				if (levelChangeId.Length == 2) {

					int levelChange = Int32.Parse(levelChangeId);

					currentCheat = "";
					cheatLevelChange = false;
					if (mapBuilder.wad.Contains(GetMapName(levelChange))) {
						BuildMap(GetMapName(levelChange));
					} else {
						Debug.Log("Couldn't find map "+GetMapName(levelChange));
					}
				}
			}
		}

		if (currentCheat == "kill") {
			GameObject.Destroy(GameObject.Find(currentMap));
			currentCheat = "";
		}

		if (currentCheat == "idclev") {
			cheatLevelChange = true;
			levelChangeId = "";
			currentCheat = "";
		}

		if (currentCheat == "test") {
			Debug.Log(currentCheat);
			currentCheat = "";
		}
	}
}
