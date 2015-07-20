﻿using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

public static class ConnectionTypes
{
	public static Type NullType { get { return typeof(ConnectionTypes); } }

	// Static consistent information about types
	static Dictionary<string, TypeData> types = new Dictionary<string, TypeData> ();
	public static TypeData GetTypeData(string typeName)
	{
		if (types == null || types.Count == 0)
			FetchTypes();
		TypeData res;
		if( types.TryGetValue(typeName, out res) )
			return res;
		UnityEngine.Debug.LogError("No TypeData defined for: " + typeName);
		return types.First().Value;
	}

	public static Type GetInputType(string typeName)
	{
		if (types == null || types.Count == 0)
			FetchTypes();
		TypeData res;
		if (types.TryGetValue(typeName, out res))
			return res.InputType ?? NullType;
		UnityEngine.Debug.LogError("No TypeData defined for: " + typeName);
		return NullType;
	}

	public static Type GetOutputType(string typeName)
	{
		if (types == null || types.Count == 0)
			FetchTypes();
		TypeData res;
		if (types.TryGetValue(typeName, out res))
			return res.OutputType ?? NullType;
		UnityEngine.Debug.LogError("No TypeData defined for: " + typeName);
		return NullType;
	}

	/// <summary>
	/// Fetches every Type Declaration in the assembly
	/// </summary>
	public static void FetchTypes () 
	{ // Search the current and (if the NodeEditor is packed into a .dll) the calling one
		types = new Dictionary<string, TypeData> ();

		Assembly assembly = Assembly.GetExecutingAssembly ();
		foreach (Type type in assembly.GetTypes ().Where (T => T.IsClass && !T.IsAbstract && T.GetInterfaces ().Contains (typeof (ITypeDeclaration)))) 
		{
			ITypeDeclaration typeDecl = assembly.CreateInstance (type.Name) as ITypeDeclaration;
			Texture2D InputKnob = LoadTexture(typeDecl.InputKnob_TexPath);
			Texture2D OutputKnob = LoadTexture(typeDecl.OutputKnob_TexPath);
			types.Add(typeDecl.name, new TypeData(typeDecl.col, InputKnob, OutputKnob, typeDecl.InputType, typeDecl.OutputType));
		}

		if (assembly != Assembly.GetCallingAssembly ())
		{
			assembly = Assembly.GetCallingAssembly ();
			foreach (Type type in assembly.GetTypes ().Where (T => T.IsClass && !T.IsAbstract && T.GetInterfaces ().Contains (typeof (ITypeDeclaration)))) 
			{
				ITypeDeclaration typeDecl = assembly.CreateInstance (type.Name) as ITypeDeclaration;
				Texture2D InputKnob = LoadTexture(typeDecl.InputKnob_TexPath);
				Texture2D OutputKnob = LoadTexture(typeDecl.OutputKnob_TexPath);
				types.Add (typeDecl.name, new TypeData (typeDecl.col, InputKnob, OutputKnob, typeDecl.InputType, typeDecl.OutputType));
			}
		}
	}

	private static Texture2D LoadTexture(string texPath)
	{
#if UNITY_EDITOR
		var fullPath = System.IO.Path.Combine(NodeEditor.editorPath, texPath);
		var resTexture = UnityEditor.AssetDatabase.LoadAssetAtPath(fullPath, typeof(Texture2D)) as Texture2D;
		if (resTexture == null)
		{
			UnityEngine.Debug.LogError(string.Format("Node_Editor: Texture not found at '{0}', did you install Node_Editor correctly in the 'Plugins' folder?", fullPath));
		}
		return resTexture;
#else
		return null;
#endif
	}
}

public struct TypeData 
{
	public Color col;
	public Texture2D InputKnob;
	public Texture2D OutputKnob;
	public Type InputType;
	public Type OutputType;
	
	public TypeData (Color color, Texture2D inKnob, Texture2D outKnob, Type inputType, Type outputType) 
	{
		col = color;
		InputKnob = NodeEditor.Tint (inKnob, color);
		OutputKnob = NodeEditor.Tint (outKnob, color);
		InputType = inputType;
		OutputType = outputType;
	}
}

public interface ITypeDeclaration
{
	string name { get; }
	Color col { get; }
	string InputKnob_TexPath { get; }
	string OutputKnob_TexPath { get; }
	Type InputType { get; }
	Type OutputType { get; }
}

// TODO: Node Editor: Built-In Connection Types
public class FloatType : ITypeDeclaration 
{
	public string name { get { return "Float"; } }
	public Color col { get { return Color.cyan; } }
	public string InputKnob_TexPath { get { return "Textures/In_Knob.png"; } }
	public string OutputKnob_TexPath { get { return "Textures/Out_Knob.png"; } }
	public Type InputType { get { return null; } }
	public Type OutputType { get { return typeof(FloatValue); } }
}

[Serializable]
public class FloatValue
{
	[NonSerialized]
	public float value;
}


