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

namespace DragAndDrop
{
    [BepInPlugin(GUID: "com.immi.koikatu.draganddrop", Name: "Drag and Drop", Version: "1.1")]
    class DragAndDrop : BaseUnityPlugin
    {
        private UnityDragAndDropHook hook;

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

            if (Singleton<Manager.Scene>.IsInstance()) {

                try {

                    if (Singleton<Manager.Scene>.Instance.NowSceneNames.Any(_ => _ == "CustomScene")) {

                        if (Singleton<CustomBase>.IsInstance()) {
                            LoadCharacter(path);
                            Utils.Sound.Play(SystemSE.ok_s);
                        }
                    }
                    else if (Singleton<Manager.Scene>.Instance.NowSceneNames.Any(_ => _ == "Studio")) {

                        if (path.Contains(@"UserData\chara")) {
                            AddChara(path);
                        }
                        else {
                            LoadScene(path);
                        }
                        Utils.Sound.Play(SystemSE.ok_s);
                    }
                }
                catch (Exception ex) {
                    BepInLogger.Log("Character load failed", true);
                    BepInLogger.Log(ex.ToString());
                    Utils.Sound.Play(SystemSE.ok_l);
                }
            }
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
