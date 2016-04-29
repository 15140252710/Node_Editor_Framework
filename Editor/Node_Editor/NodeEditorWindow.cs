﻿using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

using NodeEditorFramework;
using NodeEditorFramework.Utilities;

namespace NodeEditorFramework.Standard
{
	public class NodeEditorWindow : EditorWindow 
	{
		// Information about current instance
		private static NodeEditorWindow _editor;
		public static NodeEditorWindow editor { get { AssureEditor (); return _editor; } }
		public static void AssureEditor () { if (_editor == null) OpenNodeEditor (); }

		// Opened Canvas
		public NodeCanvas mainNodeCanvas;
		public NodeEditorState mainEditorState;
		public static NodeCanvas MainNodeCanvas { get { return editor.mainNodeCanvas; } }
		public static NodeEditorState MainEditorState { get { return editor.mainEditorState; } }
		public void AssureCanvas () { if (mainNodeCanvas == null) NewNodeCanvas (); }
		public static string openedCanvasPath;
		public static string tempSessionPath;

		// GUI
		public static int sideWindowWidth = 400;
		private static Texture iconTexture;
		public Rect sideWindowRect { get { return new Rect (position.width - sideWindowWidth, 0, sideWindowWidth, position.height); } }
		public Rect canvasWindowRect { get { return new Rect (0, 0, position.width - sideWindowWidth, position.height); } }

		#region General 

		/// <summary>
		/// Opens the Node Editor window
		/// </summary>
		[MenuItem ("Window/Node Editor")]
		public static void OpenNodeEditor () 
		{
			_editor = GetWindow<NodeEditorWindow> ();
			_editor.minSize = new Vector2 (800, 600);
			NodeEditor.initiated = NodeEditor.InitiationError = false;

			iconTexture = ResourceManager.LoadTexture (EditorGUIUtility.isProSkin? "Textures/Icon_Dark.png" : "Textures/Icon_Light.png");
			_editor.titleContent = new GUIContent ("Node Editor", iconTexture);
		}
		
		/// <summary>
		/// Handle opening canvas when double-clicking asset
		/// </summary>
		[UnityEditor.Callbacks.OnOpenAsset(1)]
		private static bool AutoOpenCanvas (int instanceID, int line) 
		{
			if (Selection.activeObject != null && Selection.activeObject.GetType () == typeof(NodeCanvas))
			{
				string NodeCanvasPath = AssetDatabase.GetAssetPath (instanceID);
				NodeEditorWindow.OpenNodeEditor ();
				EditorWindow.GetWindow<NodeEditorWindow> ().LoadNodeCanvas (NodeCanvasPath);
				return true;
			}
			return false;
		}

		private void OnEnable () 
		{
			_editor = this; // Have to set this after reload
			NodeEditor.checkInit (false);

			NodeEditor.ClientRepaints -= Repaint;
			NodeEditor.ClientRepaints += Repaint;

			EditorLoadingControl.justLeftPlayMode -= NormalReInit;
			EditorLoadingControl.justLeftPlayMode += NormalReInit;
			// Here, both justLeftPlayMode and justOpenedNewScene have to act because of timing
			EditorLoadingControl.justOpenedNewScene -= NormalReInit;
			EditorLoadingControl.justOpenedNewScene += NormalReInit;


			// Setup Cache
			tempSessionPath = Path.GetDirectoryName (AssetDatabase.GetAssetPath (MonoScript.FromScriptableObject (this)));
			LoadCache ();
			SetupCacheEvents ();
		}

		private void NormalReInit () 
		{
			NodeEditor.ReInit (false);
		}

		private void OnDestroy () 
		{
			NodeEditor.ClientRepaints -= Repaint;
			// Clear Cache
			ClearCacheEvents ();
		}

		#endregion

		#region GUI

		private void OnGUI () 
		{
			// Initiation
			NodeEditor.checkInit (true);
			if (NodeEditor.InitiationError) 
			{
				GUILayout.Label ("Node Editor Initiation failed! Check console for more information!");
				return;
			}
			AssureEditor ();
			AssureCanvas ();

			// Specify the Canvas rect in the EditorState
			mainEditorState.canvasRect = canvasWindowRect;
			// If you want to use GetRect:
//			Rect canvasRect = GUILayoutUtility.GetRect (600, 600);
//			if (Event.current.type != EventType.Layout)
//				mainEditorState.canvasRect = canvasRect;

			// Perform drawing with error-handling
			try
			{
				NodeEditor.DrawCanvas (mainNodeCanvas, mainEditorState);
			}
			catch (UnityException e)
			{ // on exceptions in drawing flush the canvas to avoid locking the ui.
				NewNodeCanvas ();
				NodeEditor.ReInit (true);
				Debug.LogError ("Unloaded Canvas due to exception when drawing!");
				Debug.LogException (e);
			}

			// Draw Side Window
			sideWindowWidth = Math.Min (600, Math.Max (200, (int)(position.width / 5)));
			NodeEditorGUI.StartNodeGUI ();
			GUILayout.BeginArea (sideWindowRect, GUI.skin.box);
			DrawSideWindow ();
			GUILayout.EndArea ();
			NodeEditorGUI.EndNodeGUI ();
		}

		private void DrawSideWindow () 
		{
			GUILayout.Label (new GUIContent ("Node Editor (" + mainNodeCanvas.name + ")", "Opened Canvas path: " + openedCanvasPath), NodeEditorGUI.nodeLabelBold);

			if (GUILayout.Button (new GUIContent ("Save Canvas", "Saves the Canvas to a Canvas Save File in the Assets Folder")))
			{
				string path = EditorUtility.SaveFilePanelInProject ("Save Node Canvas", "Node Canvas", "asset", "", NodeEditor.editorPath + "Resources/Saves/");
				if (!string.IsNullOrEmpty (path))
					SaveNodeCanvas (path);
			}

			if (GUILayout.Button (new GUIContent ("Load Canvas", "Loads the Canvas from a Canvas Save File in the Assets Folder"))) 
			{
				string path = EditorUtility.OpenFilePanel ("Load Node Canvas", NodeEditor.editorPath + "Resources/Saves/", "asset");
				if (!path.Contains (Application.dataPath)) 
				{
					if (!string.IsNullOrEmpty (path))
						ShowNotification (new GUIContent ("You should select an asset inside your project folder!"));
				}
				else
				{
					path = path.Replace (Application.dataPath, "Assets");
					LoadNodeCanvas (path);
				}
			}

			if (GUILayout.Button (new GUIContent ("New Canvas", "Loads an empty Canvas")))
				NewNodeCanvas ();

			if (GUILayout.Button (new GUIContent ("Recalculate All", "Initiates complete recalculate. Usually does not need to be triggered manually.")))
				NodeEditor.RecalculateAll (mainNodeCanvas);

			if (GUILayout.Button ("Force Re-Init"))
				NodeEditor.ReInit (true);
			
			NodeEditorGUI.knobSize = EditorGUILayout.IntSlider (new GUIContent ("Handle Size", "The size of the Node Input/Output handles"), NodeEditorGUI.knobSize, 12, 20);
			mainEditorState.zoom = EditorGUILayout.Slider (new GUIContent ("Zoom", "Use the Mousewheel. Seriously."), mainEditorState.zoom, 0.6f, 2);

			if (mainEditorState.selectedNode != null && Event.current.type != EventType.Ignore)
				mainEditorState.selectedNode.DrawNodePropertyEditor();
		}

		#endregion

		#region Cache

		private void SetupCacheEvents () 
		{
			// Load the cache after the NodeEditor was cleared

			EditorLoadingControl.lateEnteredPlayMode -= LoadCache;
			EditorLoadingControl.lateEnteredPlayMode += LoadCache;

			// included in justOpenedNewScene as playmode scene is a new, temporary scene
			//EditorLoadingControl.justLeftPlayMode -= LoadCache;
			//EditorLoadingControl.justLeftPlayMode += LoadCache;

			EditorLoadingControl.justOpenedNewScene -= LoadCache;
			EditorLoadingControl.justOpenedNewScene += LoadCache;

			// Add new objects to the cache save file

			NodeEditorCallbacks.OnAddNode -= SaveNewNode;
			NodeEditorCallbacks.OnAddNode += SaveNewNode;

			NodeEditorCallbacks.OnAddNodeKnob -= SaveNewNodeKnob;
			NodeEditorCallbacks.OnAddNodeKnob += SaveNewNodeKnob;
		}

		private void ClearCacheEvents () 
		{
			// Remove callbacks

			EditorLoadingControl.lateEnteredPlayMode -= LoadCache;
			EditorLoadingControl.justLeftPlayMode -= LoadCache;
			EditorLoadingControl.justOpenedNewScene -= LoadCache;

			NodeEditorCallbacks.OnAddNode -= SaveNewNode;
			NodeEditorCallbacks.OnAddNodeKnob -= SaveNewNodeKnob;
		}

		private string lastSessionPath { get { return tempSessionPath + "/LastSession.asset"; } }

		/// <summary>
		/// Adds the node to the cache save file it belongs to the opened canvas
		/// </summary>
		private void SaveNewNode (Node node) 
		{
			if (!mainNodeCanvas.nodes.Contains (node))
				return;
			
			NodeEditorSaveManager.AddSubAsset (node, lastSessionPath);
			foreach (ScriptableObject so in node.GetScriptableObjects ())
				NodeEditorSaveManager.AddSubAsset (so, node);
			
			foreach (NodeKnob knob in node.nodeKnobs)
			{
				NodeEditorSaveManager.AddSubAsset (knob, node);
				foreach (ScriptableObject so in knob.GetScriptableObjects ())
					NodeEditorSaveManager.AddSubAsset (so, knob);
			}

			AssetDatabase.SaveAssets ();
			AssetDatabase.Refresh ();
		}

		/// <summary>
		/// Adds the nodeKnob to the cache save file it belongs to the opened canvas
		/// </summary>
		private void SaveNewNodeKnob (NodeKnob knob) 
		{
			if (!mainNodeCanvas.nodes.Contains (knob.body))
				return;
			
			NodeEditorSaveManager.AddSubAsset (knob, knob.body);
			foreach (ScriptableObject so in knob.GetScriptableObjects ())
				NodeEditorSaveManager.AddSubAsset (so, knob);
		}

		/// <summary>
		/// Creates a new cache save file for the currently loaded canvas 
		/// Only needs to be called when a new canvas is created or loaded
		/// </summary>
		private void SaveCache () 
		{
			string canvasName = mainNodeCanvas.name;
			EditorPrefs.SetString ("NodeEditorLastSession", canvasName);

			NodeEditorSaveManager.SaveNodeCanvas (lastSessionPath, false, mainNodeCanvas, mainEditorState);
			mainNodeCanvas.name = canvasName;

			AssetDatabase.SaveAssets ();
			AssetDatabase.Refresh ();
		}

		/// <summary>
		/// Loads the canvas from the cache save file
		/// Called whenever a reload was made
		/// </summary>
		private void LoadCache () 
		{
			string lastSessionName = EditorPrefs.GetString ("NodeEditorLastSession");
			mainNodeCanvas = NodeEditorSaveManager.LoadNodeCanvas (lastSessionPath, false);
			if (mainNodeCanvas == null)
				NewNodeCanvas ();
			else 
			{
				mainNodeCanvas.name = lastSessionName;
				List<NodeEditorState> editorStates = NodeEditorSaveManager.LoadEditorStates (lastSessionPath, false);
				if (editorStates == null || editorStates.Count == 0 || (mainEditorState = editorStates.Find (x => x.name == "MainEditorState")) == null )
				{ // New NodeEditorState
					mainEditorState = CreateInstance<NodeEditorState> ();
					mainEditorState.canvas = mainNodeCanvas;
					mainEditorState.name = "MainEditorState";
					NodeEditorSaveManager.AddSubAsset (mainEditorState, lastSessionPath);
					AssetDatabase.SaveAssets ();
					AssetDatabase.Refresh ();
				}

				NodeEditor.RecalculateAll (mainNodeCanvas);
			}
		}

//		private void DeleteCache () 
//		{
//			string lastSession = EditorPrefs.GetString ("NodeEditorLastSession");
//			if (!String.IsNullOrEmpty (lastSession))
//			{
//				AssetDatabase.DeleteAsset (tempSessionPath + "/" + lastSession);
//				AssetDatabase.Refresh ();
//			}
//			EditorPrefs.DeleteKey ("NodeEditorLastSession");
//		}

		private void CheckCurrentCache () 
		{
			if (AssetDatabase.GetAssetPath (mainNodeCanvas) != lastSessionPath)
				throw new UnityException ("Cache system error: Current Canvas is not saved as the temporary cache!");
		}

		#endregion

		#region Save/Load
		
		/// <summary>
		/// Saves the mainNodeCanvas and it's associated mainEditorState as an asset at path
		/// </summary>
		public void SaveNodeCanvas (string path) 
		{
			NodeEditorSaveManager.SaveNodeCanvas (path, true, mainNodeCanvas, mainEditorState);
			Repaint ();
		}
		
		/// <summary>
		/// Loads the mainNodeCanvas and it's associated mainEditorState from an asset at path
		/// </summary>
		public void LoadNodeCanvas (string path) 
		{
			// Load the NodeCanvas
			mainNodeCanvas = NodeEditorSaveManager.LoadNodeCanvas (path, true);
			if (mainNodeCanvas == null) 
			{
				Debug.Log ("Could not load NodeCanvas from '" + path + "'!");
				NewNodeCanvas ();
				return;
			}
			// Retore or save name
			if (mainNodeCanvas.name != "lastSession")
				EditorPrefs.SetString ("NodeEditorLastSession", mainNodeCanvas.name);
			else
				mainNodeCanvas.name = EditorPrefs.GetString ("NodeEditorLastSession");
			
			// Load the associated MainEditorState
			List<NodeEditorState> editorStates = NodeEditorSaveManager.LoadEditorStates (path, false);
			if (editorStates.Count == 0) 
			{
				mainEditorState = ScriptableObject.CreateInstance<NodeEditorState> ();
				Debug.LogError ("The save file '" + path + "' did not contain an associated NodeEditorState!");
			}
			else 
			{
				mainEditorState = editorStates.Find (x => x.name == "MainEditorState");
				if (mainEditorState == null) mainEditorState = editorStates[0];
			}
			NodeEditorSaveManager.CreateWorkingCopy (ref mainEditorState);
			mainEditorState.canvas = mainNodeCanvas;

			openedCanvasPath = path;
			SaveCache ();
			NodeEditor.RecalculateAll (mainNodeCanvas);
			Repaint ();
		}

		/// <summary>
		/// Creates and opens a new empty node canvas
		/// </summary>
		public void NewNodeCanvas () 
		{
			// New NodeCanvas
			mainNodeCanvas = CreateInstance<NodeCanvas> ();
			mainNodeCanvas.name = "New Canvas";
			EditorPrefs.SetString ("NodeEditorLastSession", "New Canvas");
			// New NodeEditorState
			mainEditorState = CreateInstance<NodeEditorState> ();
			mainEditorState.canvas = mainNodeCanvas;
			mainEditorState.name = "MainEditorState";

			openedCanvasPath = "";
			SaveCache ();
		}
		
		#endregion
	}
}