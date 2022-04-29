using System;
using UnityEditor.GraphToolsFoundation.Overdrive;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.ShaderGraph.GraphUI
{
    public static class GraphAssetUtils
    {
        public class CreateGraphAssetAction : ProjectWindowCallback.EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                ShaderGraphAsset.HandleCreate(pathName);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathName);
                Selection.activeObject = obj;
            }
        }

        public class CreateSubGraphAssetAction : ProjectWindowCallback.EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                ShaderSubGraphAsset.HandleCreate(pathName);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathName);
                Selection.activeObject = obj;
            }
        }

        [MenuItem("Assets/Create/Shader Graph 2/Blank Shader Graph", priority = CoreUtils.Priorities.assetsCreateShaderMenuPriority)]
        public static void CreateBlankGraphInProjectWindow()
        {
            var newGraphAction = ScriptableObject.CreateInstance<CreateGraphAssetAction>();

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                newGraphAction,
                $"{ShaderGraphStencil.DefaultGraphAssetName}.{ShaderGraphStencil.GraphExtension}",
                null,
                null);
        }

        [MenuItem("Assets/Create/Shader Graph 2/Blank Shader SubGraph", priority = CoreUtils.Priorities.assetsCreateShaderMenuPriority)]
        public static void CreateBlankSubGraphInProjectWindow()
        {
            var newGraphAction = ScriptableObject.CreateInstance<CreateSubGraphAssetAction>();

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                newGraphAction,
                $"{ShaderGraphStencil.DefaultSubGraphAssetName}.{ShaderGraphStencil.SubGraphExtension}",
                null,
                null);
        }

        private static void SaveImplementation(BaseGraphTool GraphTool, Action<string, ShaderGraphAssetModel> SaveAction)
        {
            // If no currently opened graph, early out
            if (GraphTool.ToolState.CurrentGraph.GetGraphAsset() == null)
                return;
            if (GraphTool.ToolState.CurrentGraph.GetGraphAsset() is ShaderGraphAssetModel assetModel)
            {
                var assetPath = GraphTool.ToolState.CurrentGraph.GetGraphAssetPath();
                SaveAction(assetPath, assetModel);

                // Set to false after saving to clear modification state from editor window tab
                assetModel.Dirty = false;
            }
        }

        private static string SaveAsImplementation(BaseGraphTool GraphTool, Action<string, ShaderGraphAssetModel> SaveAction, string dialogTitle, string extension)
        {
            // If no currently opened graph, early out
            if (GraphTool.ToolState.CurrentGraph.GetGraphAsset() == null)
                return String.Empty;

            if (GraphTool.ToolState.CurrentGraph.GetGraphAsset() is ShaderGraphAssetModel assetModel)
            {
                // Get folder of current shader graph asset
                var path = GraphTool.ToolState.CurrentGraph.GetGraphAssetPath();
                path = path.Remove(path.LastIndexOf('/'));

                var destinationPath = EditorUtility.SaveFilePanel(dialogTitle, path, GraphTool.ToolState.CurrentGraph.GetGraphAsset().Name, extension);
                // If User cancelled operation or provided an invalid path
                if (string.IsNullOrEmpty(destinationPath))
                    return string.Empty;

                SaveAction(destinationPath, assetModel);

                // Refresh asset database so newly saved asset shows up
                AssetDatabase.Refresh();

                return destinationPath;
            }

            return string.Empty;
        }

        static void SaveGraphImplementation(BaseGraphTool GraphTool) =>
            SaveImplementation(GraphTool,
                ShaderGraphAsset.HandleSave);
        static void SaveSubGraphImplementation(BaseGraphTool GraphTool) =>
            SaveImplementation(GraphTool,
                ShaderSubGraphAsset.HandleSave);

        static string SaveAsGraphImplementation(BaseGraphTool GraphTool) =>
            SaveAsImplementation(GraphTool,
                ShaderGraphAsset.HandleSave,
                "Save Shader Graph Asset at: ",
                ShaderGraphStencil.GraphExtension);
        static string SaveAsSubGraphImplementation(BaseGraphTool GraphTool) =>
            SaveAsImplementation(GraphTool,
                ShaderGraphAsset.HandleSave,
                "Save Shader SubGraph Asset at:",
                ShaderGraphStencil.SubGraphExtension);

        /// <summary>
        /// Saves the graph *or* subgraph that is currently open in the given GraphTool.
        /// Does nothing if there is no open graph.
        /// </summary>
        /// <param name="graphTool">Graph tool with an open Shader Graph asset.</param>
        public static void SaveOpenGraphAsset(BaseGraphTool graphTool)
        {
            if (graphTool.ToolState.CurrentGraph.GetGraphAsset() is not ShaderGraphAssetModel graphAsset)
            {
                return;
            }

            if (graphAsset.IsSubGraph)
            {
                SaveSubGraphImplementation(graphTool);
            }
            else
            {
                SaveGraphImplementation(graphTool);
            }
        }

        /// <summary>
        /// Saves a new file from the graph *or* subgraph that is currently open in the given GraphTool.
        /// Does nothing and returns an empty string if there is no open graph.
        /// </summary>
        /// <param name="graphTool">Graph tool with an open Shader Graph asset.</param>
        public static string SaveOpenGraphAssetAs(BaseGraphTool graphTool)
        {
            if (graphTool.ToolState.CurrentGraph.GetGraphAsset() is not ShaderGraphAssetModel graphAsset)
            {
                return string.Empty;
            }

            return graphAsset.IsSubGraph
                ? SaveAsSubGraphImplementation(graphTool)
                : SaveAsGraphImplementation(graphTool);
        }
    }
}
