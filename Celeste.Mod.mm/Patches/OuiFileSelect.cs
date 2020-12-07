﻿using MonoMod;
using System;
using System.Collections;
using System.IO;

namespace Celeste {
    class patch_OuiFileSelect : OuiFileSelect {
        public float Scroll = 0f;

        [PatchOuiFileSelectSubmenuChecks] // we want to manipulate the orig method with MonoModRules
        public extern IEnumerator orig_Enter(Oui from);
        public new IEnumerator Enter(Oui from) {
            if (!Loaded) {
                // first load: we want to check how many slots there are by checking which files exist in the Saves folder.
                int maxSaveFile = 1; // we're adding 2 later, so there will be at least 3 slots.
                string saveFilePath = patch_UserIO.GetSaveFilePath();
                if (Directory.Exists(saveFilePath)) {
                    foreach (string filePath in Directory.GetFiles(saveFilePath)) {
                        string fileName = Path.GetFileName(filePath);
                        // is the file named [number].celeste?
                        if (fileName.EndsWith(".celeste") && int.TryParse(fileName.Substring(0, fileName.Length - 8), out int fileIndex)) {
                            maxSaveFile = Math.Max(maxSaveFile, fileIndex);
                        }
                    }
                }

                // if 2.celeste exists, slot 3 is the last slot filled, therefore we want 4 slots (2 + 2) to always have the latest one empty.
                Slots = new OuiFileSelectSlot[maxSaveFile + 2];
            }

            int slotIndex = 0;
            IEnumerator orig = orig_Enter(from);
            while (orig.MoveNext()) {
                if (orig.Current is float f && f == 0.02f) {
                    // only apply the delay if the slot is on-screen (less than 2 slots away from the selected one).
                    if (Math.Abs(SlotIndex - slotIndex) <= 2) {
                        yield return orig.Current;
                    }
                    slotIndex++;
                } else {
                    yield return orig.Current;
                }
            }
        }

        [PatchOuiFileSelectSubmenuChecks] // we want to manipulate the orig method with MonoModRules
        public extern IEnumerator orig_Leave(Oui next);
        public new IEnumerator Leave(Oui from) {
            int slotIndex = 0;
            IEnumerator orig = orig_Leave(from);
            while (orig.MoveNext()) {
                if (orig.Current is float f && f == 0.02f) {
                    // only apply the delay if the slot is on-screen (less than 2 slots away from the selected one).
                    if (Math.Abs(SlotIndex - slotIndex) <= 2) {
                        yield return orig.Current;
                    }
                    slotIndex++;
                } else {
                    yield return orig.Current;
                }
            }
        }

#pragma warning disable CS0626 // extern method with no attribute
        public extern void orig_Update();
#pragma warning restore CS0626
        public override void Update() {
            int initialFileIndex = SlotIndex;

            orig_Update();

            if (SlotIndex != initialFileIndex) {
                // selection moved, so update the Y position of all file slots.
                foreach (OuiFileSelectSlot slot in Slots) {
                    (slot as patch_OuiFileSelectSlot).ScrollTo(slot.IdlePosition.X, slot.IdlePosition.Y);
                }
            }
        }
    }
}
