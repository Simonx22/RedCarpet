﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syroot.NintenTools.Byaml;
using Syroot.NintenTools.Byaml.Dynamic;
using Syroot.NintenTools.Yaz0;
using System.IO;
using RedCarpet.Gfx;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using static RedCarpet.Object;
using Syroot.NintenTools.Bfres;
using System.Diagnostics;

/* -- RedCarpet --
 * MasterF0x
 * OatmealDome
 * Ray Koopa
 * Exelix :D
 */

namespace RedCarpet
{
    public partial class Form1 : Form
    {
        private SmShaderProgram shaderProgram;
        private SmCamera camera = new SmCamera();
        private Dictionary<string, SmModel> modelDict = new Dictionary<string, SmModel>();
        private Matrix4 projectionMatrix;
        public List<MapObject> actorList = new List<MapObject>();

        private int prevMouseX;
        private int prevMouseY;
        
        private Object loadedMap = null;

        private static Vector4 blackColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        private static Vector4 whiteColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        private static Vector4 orangeColor = new Vector4(1.0f, 0.5f, 0.2f, 1.0f);

        private Dictionary<string, byte[]> LoadedSarc = null;
        static string BASEPATH = @"C:\Users\ronal\Desktop\3DWorldKit\SM3DW\content\"; //no need to put the editor in the game's folder, \ at the end matters !!!

        public Form1()
        {
            InitializeComponent();
            if (!Directory.Exists(BASEPATH)) throw new Exception("set BASEPATH to the game's folder");
        }

        /*private void button1_Click(object sender, EventArgs e)
        {
            dynamic byaml = ByamlFile.Load("test.byml");
            IList<dynamic> objs = byaml["Objs"];
            dynamic obj3 = objs[0];
            //objs.Insert(10, obj3);
            ByamlFile.Save("new.byml", byaml);
        }*/

        private void titleDemo00StageDesign1ToolStripMenuItem_Click(object sender, EventArgs e)
        {

            LoadLevel(BASEPATH + "StageData/" + sender.ToString() + ".szs");
        }

        public void DisposeCurrentLevel()
        {
            modelDict.Clear();
            if (LoadedSarc != null) LoadedSarc.Clear();
            LoadedSarc = null;
            cpath.Text = "";
            objectsList.Items.Clear();
            if (loadedMap != null) loadedMap.mobjs.Clear();
            loadedMap = null;
            glControl1.Invalidate();
            GC.Collect();
        }

        public void LoadLevel(string filename)
        {
            DisposeCurrentLevel();
            //both yaz0 decompression and sarc unpacking are done in ram, this avoids useless wirtes to disk, faster level loading
            SARC sarc = new SARC();
            MemoryStream DecompressedSarc = new MemoryStream();
            Yaz0Compression.Decompress(filename, DecompressedSarc);
            LoadedSarc = sarc.unpackRam(DecompressedSarc); //the current level files are now stored in LoadedSarc

            /*Yaz0Compression.Decompress(BASEPATH + "StageData/" + "TitleDemo00StageDesign1" + ".szs", "stageDesign.sarc");
            sarc.unpack("stageDesign");
            Yaz0Compression.Decompress(BASEPATH + "ObjectData/" + "TitleDemoStepA" + ".szs", "stageModel.sarc");
            sarc.unpack("stageModel"); Not needed for now*/
            
            //removed stage model loading, every stage include the model name in the byml

            // parse byaml
            parseBYML(Path.GetFileNameWithoutExtension(filename) + ".byml");

            // force render
            glControl1.Invalidate();
        }

        public void parseBYML(string name)
        {
            //calling it Object wasn't a great idea, i stared at the code for half hour before realizing that it's a custom class lol
            loadedMap = new Object();
            string bymlName = "";
            if (name.EndsWith("Map1.byml"))  //the szs name always ends with 1, but the map byml doesn't, this seems to be true for every level
                bymlName = name.Replace("Map1.byml", "Map.byml");
            else if (name.EndsWith("Design1.byml"))
                bymlName = name.Replace("Design1.byml", "Design.byml");
            else if (name.EndsWith("Sound1.byml"))
                bymlName = name.Replace("Sound1.byml", "Sound.byml");
            else bymlName = name;

            dynamic byml = ByamlFile.Load(new MemoryStream(LoadedSarc[bymlName])); 

            IList<dynamic> objs = byml["Objs"];
            cpath.Text = byml["FilePath"];
            for (int i = 0; i < objs.Count; i++)
            { 
                loadedMap.mpobj = new Object.MapObject();
                loadedMap.mpobj.objectID = objs[i]["Id"];
                loadedMap.mpobj.modelName = objs[i]["ModelName"];
                loadedMap.mpobj.Layer = objs[i]["LayerConfigName"];
                loadedMap.mpobj.unitConfigName = objs[i]["UnitConfigName"];
                loadedMap.mpobj.position = new Vector3(objs[i]["Translate"]["X"], objs[i]["Translate"]["Y"], objs[i]["Translate"]["Z"]);
                loadedMap.mpobj.rotation = new Vector3(objs[i]["Rotate"]["X"], objs[i]["Rotate"]["Y"], objs[i]["Rotate"]["Z"]);
                loadedMap.mpobj.scale = new Vector3(objs[i]["Scale"]["X"], objs[i]["Scale"]["Y"], objs[i]["Scale"]["Z"]);
                loadedMap.mobjs.Add(loadedMap.mpobj);
                actorList.Add(loadedMap.mpobj);
                objectsList.Items.Add(loadedMap.mpobj.unitConfigName);

                // Load the model
                if (loadedMap.mpobj.modelName != null && !loadedMap.mpobj.Equals(""))
                {
                    LoadModel(loadedMap.mpobj.modelName);
                }
                else
                {
                    LoadModel(loadedMap.mpobj.unitConfigName);
                }
            }
        }

        private void LoadModel(string modelName)
        {
            // todo: don't hardcode
            string modelPath = BASEPATH + @"ObjectData\";
            string stagePath = BASEPATH + @"stageModel\";

            if (!Directory.Exists("Models")) Directory.CreateDirectory("Models"); //Models once unpacked will be saved in the editor's directory instead of the game folder

            // Check if the model is loaded first
            SmModel model;
            modelDict.TryGetValue(modelName, out model);
            if (model != null)
            {
                return;
            }

            if (LoadModelWithBase(modelPath, modelName))
            {
                return;
            }
            else if (LoadModelWithBase(stagePath, modelName))
            {
                return;
            }

            Console.WriteLine("WARN: Could not load a model for " + modelName);
        }
        
        private bool LoadModelWithBase(string basePath, string modelName)
        {
            // Attempt to load the bfres that contains the model
            string modelPath = "Models\\" + modelName + ".bfres";
            if (File.Exists(modelPath))
            {
                // Load the bfres
                LoadBfres(modelPath);

                return true;
            }

            // Check if the szs archive that contains the bfres exists
            string szsPath = basePath + modelName + ".szs";
            if (File.Exists(szsPath))
            {
                // Decompress the szs into a sarc archive
                string sarcPath = basePath + modelName;
                MemoryStream mem = new MemoryStream();
                Yaz0Compression.Decompress(szsPath, mem);
                SARC sarc = new SARC();
                var unpackedmodel = sarc.unpackRam(mem);
                if (!unpackedmodel.ContainsKey(modelName + ".bfres"))
                    return false;

                File.WriteAllBytes(modelPath, unpackedmodel[modelName + ".bfres"]);
                // Load the bfres
                LoadBfres(modelPath);

                return true;
            }

            return false;
        }

        private void LoadBfres(string path)
        {
            ResFile resFile = new ResFile(path);
            foreach (String key in resFile.Models.Keys)
            {
                Model model = resFile.Models[key];

                //Console.WriteLine("loading fmdl @ " + resFile.Models.IndexOf(key) + ": " + key);
                if (modelDict.ContainsKey(key))
                {
                    Console.WriteLine("WARN: Duplicated FMDL " + key + ", skipping");
                    return;
                }

                modelDict.Add(key, new SmModel(model, resFile.ByteOrder));
            }
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            // Enable depth test
            GL.Enable(EnableCap.DepthTest);

            // Set the viewport
            GL.Viewport(glControl1.ClientRectangle);

            // Compile and link shaders
            string vertexShaderSrc = Encoding.ASCII.GetString(Properties.Resources.VertexShader);
            string fragmentShaderSrc = Encoding.ASCII.GetString(Properties.Resources.FragmentShader);
            shaderProgram = new SmShaderProgram(vertexShaderSrc, fragmentShaderSrc);

            // Construct the projection matrix
            projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), (float)glControl1.Width / (float)glControl1.Height, 1.0f, 20000.0f);
        }

        private void glControl1_resize(object sender, EventArgs e)
        {
            // Update the viewport
            GL.Viewport(glControl1.ClientRectangle);
            // Construct the projection matrix
            projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), (float)glControl1.Width / (float)glControl1.Height, 1.0f, 20000.0f);
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            // Do standard clearing
            GL.ClearColor(Color.Turquoise);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Don't do anything else if there is no map loaded
            if (loadedMap == null)
            {
                glControl1.SwapBuffers();
                return;
            }

            // Get uniform locations
            int modelLocation = shaderProgram.GetUniformLocation("model");
            int viewLocation = shaderProgram.GetUniformLocation("view");
            int projectionLocation = shaderProgram.GetUniformLocation("projection");
            int colorLocation = shaderProgram.GetUniformLocation("colorVec");

            // Set uniforms
            Matrix4 viewMatrix = camera.ToMatrix4();

            shaderProgram.Use();
            GL.UniformMatrix4(viewLocation, false, ref viewMatrix);
            GL.UniformMatrix4(projectionLocation, false, ref projectionMatrix);

            // Render all map objects
            foreach (MapObject mapObject in loadedMap.mobjs)
            {
                RenderMapObject(mapObject, modelLocation, colorLocation);
            }

            // Swap buffers
            glControl1.SwapBuffers();
        }

        private void RenderMapObject(MapObject mapObject, int modelLocation, int colorLocation)
        {
            // Try to get the model via the UnitConfigName or ModelName
            SmModel model;
            if (!modelDict.TryGetValue(mapObject.unitConfigName, out model))
            {
                if (mapObject.modelName == null || !modelDict.TryGetValue(mapObject.modelName, out model))
                {
                    // Give up
                    return;
                }
            }

            // Get the position and rotation of the object
            Matrix4 positionMat = Matrix4.CreateTranslation(mapObject.position);
            Matrix4 rotXMat = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(mapObject.rotation.X));
            Matrix4 rotYMat = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(mapObject.rotation.Y));
            Matrix4 rotZMat = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(mapObject.rotation.Z));
            Matrix4 scaleMat = Matrix4.CreateScale(mapObject.scale);
            Matrix4 finalMat = scaleMat * (rotXMat * rotYMat * rotZMat) * positionMat;

            // Set the position in the shader
            GL.UniformMatrix4(modelLocation, false, ref positionMat);

            // Render filled triangles
            GL.Uniform4(colorLocation, mapObject.objectID.Equals("stageObject") ? whiteColor : whiteColor);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1, 1);

            model.Render();

            // Render outlined triangles
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Uniform4(colorLocation, blackColor);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.Enable(EnableCap.PolygonOffsetLine);
            GL.PolygonOffset(-1, -1);

            model.Render();
            mapObject.vertices = model.objVerts;

            mapObject.calcBBMin();
            mapObject.calcBBMax();

            GL.Disable(EnableCap.PolygonOffsetLine);
        }

        private void glControl1_MouseMove(object sender, MouseEventArgs e)
        {
            // OpenGL's Y-origin starts at the bottom left, unlike WinForms
            int newY = glControl1.Height - e.Y;

            if (e.Button == MouseButtons.Right)
            {
                float deltaX = ((float)e.X - (float)prevMouseX) / 100;
                float deltaY = ((float)newY - (float)prevMouseY) / 100;

                camera.yaw += deltaX;
                camera.pitch += deltaY;

                glControl1.Invalidate();
            }

            prevMouseX = e.X;
            prevMouseY = newY;
        }

        private void glControl1_MouseWheel(object sender, MouseEventArgs e)
        {
            // Move the camera towards where it's facing
            camera.cameraPosition += camera.cameraFront * e.Delta;

            glControl1.Invalidate();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void bymlViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog opn = new OpenFileDialog();
            opn.Filter = "byml files, szs files |*.byml;*.szs";
            if (opn.ShowDialog() != DialogResult.OK) return;
            dynamic byml = null;
            if (opn.FileName.EndsWith("byml")) byml = ByamlFile.Load(opn.FileName);
            else if (opn.FileName.EndsWith("szs"))
            {
                SARC sarc = new SARC();
                MemoryStream DecompressedSarc = new MemoryStream();
                Yaz0Compression.Decompress(opn.FileName, DecompressedSarc);
                var unpackedsarc = sarc.unpackRam(DecompressedSarc);
                string bymlName = Path.GetFileNameWithoutExtension(opn.FileName) + ".byml";
                if (bymlName.EndsWith("Map1.byml"))  //the szs name always ends with 1, but the map byml doesn't, this seems to be true for every level
                    bymlName = bymlName.Replace("Map1.byml", "Map.byml");
                else if (bymlName.EndsWith("Design1.byml"))
                    bymlName = bymlName.Replace("Design1.byml", "Design.byml");
                else if (bymlName.EndsWith("Sound1.byml"))
                    bymlName = bymlName.Replace("Sound1.byml", "Sound.byml");
                byml = ByamlFile.Load(new MemoryStream(unpackedsarc[bymlName]));
            }
            else throw new Exception("Not supported");
            if (byml is Dictionary<string, dynamic>) new ByamlViewer(byml).Show(); else throw new Exception("Not supported");
        }

        private void openLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog opn = new OpenFileDialog();
            opn.Filter = "szs files | *.szs";
            if (opn.ShowDialog() != DialogResult.OK) return;
            LoadLevel(opn.FileName);
        }

        private void closeCurrentLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisposeCurrentLevel();
        }

        private void glControl1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int iY = glControl1.Height - e.Y;
                Object.MapObject obj;
                obj = camera.castRay(e.X, e.Y, glControl1.Width, glControl1.Height, projectionMatrix, actorList);
                if (obj == null) return;
                MessageBox.Show(obj.unitConfigName);
            }
        }
    }
}