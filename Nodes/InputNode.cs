﻿using UnityEngine;
using UnityEditor;
using System.Collections;

[System.Serializable]
public class InputNode : Node 
{
	public const string ID = "inputNode";
	public override string GetID { get { return ID; } }

	public float value = 1f;

	public override Node Create (Vector2 pos) 
	{ // This function has to be registered in Node_Editor.ContextCallback
		InputNode node = CreateInstance <InputNode> ();
		
		node.name = "Input Node";
		node.rect = new Rect (pos.x, pos.y, 200, 50);;
		
		NodeOutput.Create (node, "Value", "Float");

		return node;
	}

	public override void NodeGUI () 
	{
		value = EditorGUILayout.FloatField (new GUIContent ("Value", "The input value of type float"), value);
		if (Event.current.type == EventType.Repaint) 
			Outputs [0].SetRect (GUILayoutUtility.GetLastRect ());

		if (GUI.changed)
			NodeEditor.RecalculateFrom (this);
	}
	
	public override bool Calculate () 
	{
		Outputs [0].value = value;
		return true;
	}
}