using B83.Win32;
using BepInEx;
using ChaCustom;
using Illusion.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Studio;
using System.Reflection;
//using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace DragAndDrop
{
    public class DragAndDropException : Exception
    {
        public DragAndDropException() { }
        public DragAndDropException(string message) : base(message) { }
        public DragAndDropException(string message, Exception inner) : base(message, inner) { }
    }

    [BepInPlugin(GUID: "com.immi.koikatu.draganddrop", Name: "Drag and Drop", Version: "1.1.3")]
    class DragAndDrop : BaseUnityPlugin
    {
        private UnityDragAndDropHook hook;

        private const string charaToken = "【KoiKatuChara】";
        private const string studioToken = "【KStudio】";
        private enum PngType { Unknown = 0, KoikatuChara, KStudio }

        private void OnEnable()
        {

            hook = new UnityDragAndDropHook();
            hook.InstallHook();
            hook.OnDroppedFiles += OnDroppedFiles;
        }

        private void OnDisable()
        {
            hook.UninstallHook();
        }

 

        private void OnDroppedFiles(List<string> aFiles, POINT aPos)
        {
            if (aFiles.Count == 0) return;
            var path = aFiles[0];
            if (path == null) return;

            if (Path.GetExtension(path).ToLower() != ".png") {
                return;
            }

            if (Singleton<Manager.Scene>.IsInstance()) {

                try {

                    PngType ptype = CheckPngType(path);

                    if (Singleton<Manager.Scene>.Instance.NowSceneNames.Any(_ => _ == "CustomScene")) {

                        if (Singleton<CustomBase>.IsInstance() && ptype == PngType.KoikatuChara) {
                            LoadCharacter(path);
                            Utils.Sound.Play(SystemSE.ok_s);
                        }
                    }
                    else if (Singleton<Manager.Scene>.Instance.NowSceneNames.Any(_ => _ == "Studio")) {

                        if (ptype == PngType.KoikatuChara) {
                            AddChara(path);
                            Utils.Sound.Play(SystemSE.ok_s);
                        }
                        else if (ptype == PngType.KStudio) {
                            LoadScene(path);
                            Utils.Sound.Play(SystemSE.ok_s);
                        }
                    }

                    if (ptype == PngType.Unknown) {
                        Utils.Sound.Play(SystemSE.ok_l);
                    }
                }
                catch (Exception ex) {
                    BepInLogger.Log("Character load failed", true);
                    BepInLogger.Log(ex.ToString());
                    Utils.Sound.Play(SystemSE.ok_l);
                }
            }
        }

        private PngType CheckPngType(string path)
        {

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs)) {
                try {
                    PngFile.SkipPng(br);
                    br.ReadInt32(); // data Version
                } catch (EndOfStreamException) {
                    return PngType.Unknown;
                }
                try {
                    if (br.ReadString() == charaToken) {
                        return PngType.KoikatuChara;
                    }
                }
                catch (EndOfStreamException) { } // unknown or scene

                int len = Encoding.UTF8.GetByteCount(studioToken);
                br.BaseStream.Seek(-len, SeekOrigin.End);
                try {
                    
                    if (Encoding.UTF8.GetString(br.ReadBytes(len)) == studioToken) {
                        return PngType.KStudio;
                    }
                }
                catch (EndOfStreamException) { } // detected unknown
            }
            return PngType.Unknown;
        }


        private void AddChara(string path)
        {
            ChaFileControl charaCtrl = new ChaFileControl();
            if (charaCtrl.LoadCharaFile(path, 1, true, true)) {
                ObjectCtrlInfo ctrlInfo = Studio.Studio.GetCtrlInfo(Singleton<Studio.Studio>.Instance.treeNodeCtrl.selectNode);
                OCIChar ocichar = ctrlInfo as OCIChar;

                if (ocichar != null && charaCtrl.parameter.sex == ocichar.sex) {
                    OCIChar[] array = (from v in Singleton<GuideObjectManager>.Instance.selectObjectKey
                                       select Studio.Studio.GetCtrlInfo(v) as OCIChar into v
                                       where v != null
                                       where v.oiCharInfo.sex == charaCtrl.parameter.sex
                                       select v).ToArray<OCIChar>();

                    for (int i = 0, num = array.Length; i < num; i++) {
                        array[i].ChangeChara(path);
                    }
                } else {

                    if (charaCtrl.parameter.sex == 0) {
                        Singleton<Studio.Studio>.Instance.AddMale(path);
                    }
                    else if (charaCtrl.parameter.sex == 1) {
                        Singleton<Studio.Studio>.Instance.AddFemale(path);
                    }
                }
            }
        }

        private void LoadScene(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            StartCoroutine(Singleton<Studio.Studio>.Instance.LoadSceneCoroutine(path));

        }

        private void LoadCharacter(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));


            LoadFlags lf = GetLoadFlags();

            var chaCtrl = Singleton<CustomBase>.Instance.chaCtrl;
            var chaFile = chaCtrl.chaFile;

            var originalSex = chaCtrl.sex;
            chaFile.LoadFileLimited(path, chaCtrl.sex, lf.face, lf.body, lf.hair, lf.parameters, lf.clothes);

            if (chaFile.GetLastErrorCode() != 0)
                throw new IOException();

            if (chaFile.parameter.sex != originalSex)
            {
                chaFile.parameter.sex = originalSex;
                BepInLogger.Log("Warning: The character's sex has been altered to match the editor mode.", true);
            }
            chaCtrl.ChangeCoordinateType(true);

            chaCtrl.Reload(!lf.clothes, !lf.face, !lf.hair, !lf.body);

            Singleton<CustomBase>.Instance.updateCustomUI = true;
            Singleton<CustomHistory>.Instance.Add5(chaCtrl, chaCtrl.Reload, false, false, false, false);
        }

        private LoadFlags GetLoadFlags()
        {
            var lf = new LoadFlags();
            lf.body = lf.clothes = lf.hair = lf.face = lf.parameters = true;

            foreach (CustomFileWindow cfw in GameObject.FindObjectsOfType<CustomFileWindow>())
            {
                if (cfw.fwType == CustomFileWindow.FileWindowType.CharaLoad)
                {
                    lf.body = cfw.tglChaLoadBody.isOn;
                    lf.clothes = cfw.tglChaLoadCoorde.isOn;
                    lf.hair = cfw.tglChaLoadHair.isOn;
                    lf.face = cfw.tglChaLoadFace.isOn;
                    lf.parameters = cfw.tglChaLoadParam.isOn;

                    break;
                }
            }

            return lf;
        }
    }

    struct LoadFlags
    {
        public bool clothes;
        public bool face;
        public bool hair;
        public bool body;
        public bool parameters;
    }
}
