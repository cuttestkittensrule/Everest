﻿using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Celeste.Mod {
    /// <summary>
    /// Your Everest main mod class inherits from this class.
    /// </summary>
    public abstract class EverestModule {

        /// <summary>
        /// Used by Everest itself to store any module metadata.
        /// 
        /// The metadata is usually parsed from meta.yaml in the archive.
        /// 
        /// You can override this property to provide dynamic metadata at runtime.
        /// Doing so isn't advised though unless you absolutely know what you're doing.
        /// Note that this doesn't affect mod loading.
        /// </summary>
        public virtual EverestModuleMetadata Metadata { get; set; }

        /// <summary>
        /// The type used for the settings object. Used for serialization, among other things.
        /// </summary>
        public virtual Type SettingsType => null;
        /// <summary>
        /// Any settings stored across runs. Everest loads this before Load gets invoked.
        /// Define your custom property returning _Settings typecasted as your custom settings type.
        /// </summary>
        public virtual EverestModuleSettings _Settings { get; set; }

        /// <summary>
        /// The type used for the save data object. Used for serialization, among other things.
        /// </summary>
        public virtual Type SaveDataType => null;
        /// <summary>
        /// Any save data stored across runs.
        /// Define your custom property returning _SaveData typecasted as your custom save data type.
        /// </summary>
        public virtual EverestModuleSaveData _SaveData { get; set; }

        /// <summary>
        /// Whether the save and session data can be saved asynchronously and separately by Everest.
        /// Doing so will use [Read,Write,Deserialize,Serialize][SaveData,Session].
        /// Otherwise, the obsolete and forcibly synchronous [Load,Save,Delete] methods will be used.
        /// Defaults to true; automatically gets set to false if you override an old method without overriding any new one.
        /// </summary>
        public virtual bool SaveDataAsync { get; set; }
        private bool ForceSaveDataAsync;

        /// <summary>
        /// The type used for the session object. Used for serialization, among other things.
        /// </summary>
        public virtual Type SessionType => null;
        /// <summary>
        /// Any save data stored for the current session.
        /// Define your custom property returning _Session typecasted as your custom session type.
        /// </summary>
        public virtual EverestModuleSession _Session { get; set; }

        public EverestModule() {
            // Default to async as long as all old methods stay the same.
            SaveDataAsync |=
                GetType().GetMethod(nameof(LoadSaveData)) == typeof(EverestModule).GetMethod(nameof(LoadSaveData)) &&
                GetType().GetMethod(nameof(SaveSaveData)) == typeof(EverestModule).GetMethod(nameof(SaveSaveData)) &&
                GetType().GetMethod(nameof(DeleteSaveData)) == typeof(EverestModule).GetMethod(nameof(DeleteSaveData)) &&
                GetType().GetMethod(nameof(LoadSession)) == typeof(EverestModule).GetMethod(nameof(LoadSession)) &&
                GetType().GetMethod(nameof(SaveSession)) == typeof(EverestModule).GetMethod(nameof(SaveSession)) &&
                GetType().GetMethod(nameof(DeleteSession)) == typeof(EverestModule).GetMethod(nameof(DeleteSession));
            // Prefer async if the mod overrides any new method.
            SaveDataAsync |=
                GetType().GetMethod(nameof(ReadSaveData)) != typeof(EverestModule).GetMethod(nameof(ReadSaveData)) ||
                GetType().GetMethod(nameof(DeserializeSaveData)) != typeof(EverestModule).GetMethod(nameof(DeserializeSaveData)) ||
                GetType().GetMethod(nameof(SerializeSaveData)) != typeof(EverestModule).GetMethod(nameof(SerializeSaveData)) ||
                GetType().GetMethod(nameof(WriteSaveData)) != typeof(EverestModule).GetMethod(nameof(WriteSaveData)) ||
                GetType().GetMethod(nameof(ReadSession)) != typeof(EverestModule).GetMethod(nameof(ReadSession)) ||
                GetType().GetMethod(nameof(DeserializeSession)) != typeof(EverestModule).GetMethod(nameof(DeserializeSession)) ||
                GetType().GetMethod(nameof(SerializeSession)) != typeof(EverestModule).GetMethod(nameof(SerializeSession)) ||
                GetType().GetMethod(nameof(WriteSession)) != typeof(EverestModule).GetMethod(nameof(WriteSession));
            if (!SaveDataAsync)
                Logger.Log(LogLevel.Warn, "EverestModule", $"{Metadata.Name} doesn't support save data async IO!");
        }

        /// <summary>
        /// Load the mod settings. Loads the settings from {UserIO.GetSavePath("Saves")}/modsettings-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void LoadSettings() {
            if (SettingsType == null)
                return;

            _Settings = (EverestModuleSettings) SettingsType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);

            string path = patch_UserIO.GetSaveFilePath("modsettings-" + Metadata.Name);

            // Temporary fallback to help migrate settings from their old location.
            if (!File.Exists(path))
                path = Path.Combine(Everest.PathEverest, "ModSettings-OBSOLETE", Metadata.Name + ".yaml");

            if (!File.Exists(path))
                return;

            try {
                using (Stream stream = File.OpenRead(path)) {
                    if (_Settings is EverestModuleBinarySettings) {
                        using (BinaryReader reader = new BinaryReader(stream))
                            ((EverestModuleBinarySettings) _Settings).Read(reader);
                    } else {
                        using (StreamReader reader = new StreamReader(stream))
                            YamlHelper.DeserializerUsing(_Settings).Deserialize(reader, SettingsType);
                    }
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to load the settings of {Metadata.Name}!");
                Logger.LogDetailed(e);
            }

            if (_Settings == null)
                _Settings = (EverestModuleSettings) SettingsType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
        }

        /// <summary>
        /// Save the mod settings. Saves the settings to {UserIO.GetSavePath("Saves")}/modsettings-{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void SaveSettings() {
            if (SettingsType == null || _Settings == null)
                return;

            string path = patch_UserIO.GetSaveFilePath("modsettings-" + Metadata.Name);
            if (File.Exists(path))
                File.Delete(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try {
                using (FileStream stream = File.OpenWrite(path)) {
                    if (_Settings is EverestModuleBinarySettings) {
                        using (BinaryWriter writer = new BinaryWriter(stream)) {
                            ((EverestModuleBinarySettings) _Settings).Write(writer);
                            stream.Flush(true);
                        }
                    } else {
                        using (StreamWriter writer = new StreamWriter(stream)) {
                            YamlHelper.Serializer.Serialize(writer, _Settings, SettingsType);
                            stream.Flush(true);
                        }
                    }
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to save the settings of {Metadata.Name}!");
                Logger.LogDetailed(e);
            }
        }

        /// <summary>
        /// Load the mod save data. Loads the save data from {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        [Obsolete("Override DeserializeSaveData and ReadSaveData instead.")]
        public virtual void LoadSaveData(int index) {
            ForceSaveDataAsync = true;
            DeserializeSaveData(index, ReadSaveData(index));
            ForceSaveDataAsync = false;
        }

        /// <summary>
        /// Save the mod save data. Saves the save data to {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        /// 
        [Obsolete("Override SerializeSaveData and WriteSaveData instead.")]
        public virtual void SaveSaveData(int index) {
            ForceSaveDataAsync = true;
            WriteSaveData(index, SerializeSaveData(index));
            ForceSaveDataAsync = false;
        }

        /// <summary>
        /// Delete the mod save data. Deletes the save data at {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        [Obsolete("Override WriteSaveData and handle null data instead.")]
        public virtual void DeleteSaveData(int index) {
            ForceSaveDataAsync = true;
            WriteSaveData(index, null);
            ForceSaveDataAsync = false;
        }

        /// <summary>
        /// Read the mod save data bytes from a file, {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual byte[] ReadSaveData(int index) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SaveDataType == null)
                return null;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsave-" + Metadata.Name);
            if (!File.Exists(path))
                return null;

            try {
                return File.ReadAllBytes(path);
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to read the save data of {Metadata.Name}!");
                Logger.LogDetailed(e);
                return null;
            }
        }

        /// <summary>
        /// Write the mod save data bytes into a file, {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsave-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void WriteSaveData(int index, byte[] data) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SaveDataType == null)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsave-" + Metadata.Name);
            if (File.Exists(path))
                File.Delete(path);

            if (data == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try {
                using (FileStream stream = File.OpenWrite(path)) {
                    stream.Write(data, 0, data.Length);
                    if (SaveDataAsync)
                        stream.Flush(true);
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to write the save data of {Metadata.Name}!");
                Logger.LogDetailed(e);
            }
        }

        /// <summary>
        /// Deserialize the mod save data from its raw bytes, fed with data from ReadSaveData either immediately or async.
        /// </summary>
        public virtual void DeserializeSaveData(int index, byte[] data) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SaveDataType == null)
                return;

            _SaveData = (EverestModuleSaveData) SaveDataType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
            _SaveData.Index = index;

            if (data == null)
                return;

            try {
                using (MemoryStream stream = new MemoryStream(data)) {
                    if (_SaveData is EverestModuleBinarySaveData bsd) {
                        using (BinaryReader reader = new BinaryReader(stream))
                            bsd.Read(reader);
                    } else {
                        using (StreamReader reader = new StreamReader(stream))
                            YamlHelper.DeserializerUsing(_SaveData).Deserialize(reader, SaveDataType);
                    }
                }
                _SaveData.Index = index;
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to deserialize the save data of {Metadata.Name}!");
                Logger.LogDetailed(e);
            }
        }

        /// <summary>
        /// Serialize the mod save data into its raw bytes, to be fed into WriteSaveData immediately or async.
        /// </summary>
        public virtual byte[] SerializeSaveData(int index) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SaveDataType == null)
                return null;

            try {
                using (MemoryStream stream = new MemoryStream()) {
                    if (_SaveData is EverestModuleBinarySaveData bsd) {
                        using (BinaryWriter writer = new BinaryWriter(new UndisposableStream(stream)))
                            bsd.Write(writer);
                    } else {
                        using (StreamWriter writer = new StreamWriter(new UndisposableStream(stream)))
                            YamlHelper.Serializer.Serialize(writer, _SaveData, SaveDataType);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                    return stream.ToArray();
                }

            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to serialize the save data of {Metadata.Name}!");
                Logger.LogDetailed(e);
                return null;
            }
        }

        /// <summary>
        /// Load the mod session. Loads the session from {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        [Obsolete("Override DeserializeSession and ReadSession instead.")]
        public virtual void LoadSession(int index, bool forceNew) {
            ForceSaveDataAsync = true;
            DeserializeSession(index, forceNew ? null : ReadSession(index));
            ForceSaveDataAsync = false;
        }

        /// <summary>
        /// Save the mod session. Saves the session to {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        [Obsolete("Override SerializeSession and WriteSession instead.")]
        public virtual void SaveSession(int index) {
            ForceSaveDataAsync = true;
            WriteSession(index, SerializeSession(index));
            ForceSaveDataAsync = false;
        }

        /// <summary>
        /// Delete the mod session. Deletes the session at {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        [Obsolete("Override WriteSession and handle null data instead.")]
        public virtual void DeleteSession(int index) {
            ForceSaveDataAsync = true;
            WriteSession(index, null);
            ForceSaveDataAsync = false;
        }

        /// <summary>
        /// Read the mod session bytes from a file, {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual byte[] ReadSession(int index) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SessionType == null)
                return null;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsession-" + Metadata.Name);
            if (!File.Exists(path))
                return null;

            try {
                return File.ReadAllBytes(path);
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to read the session of {Metadata.Name}!");
                Logger.LogDetailed(e);
                return null;
            }
        }

        /// <summary>
        /// Write the mod session bytes into a file, {UserIO.GetSavePath("Saves")}/{SaveData.GetFilename(index)}-modsession-{Metadata.Name}.celeste by default.
        /// </summary>
        public virtual void WriteSession(int index, byte[] data) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SessionType == null)
                return;

            string path = patch_UserIO.GetSaveFilePath(patch_SaveData.GetFilename(index) + "-modsession-" + Metadata.Name);
            if (File.Exists(path))
                File.Delete(path);

            if (data == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try {
                using (FileStream stream = File.OpenWrite(path)) {
                    stream.Write(data, 0, data.Length);
                    if (SaveDataAsync)
                        stream.Flush(true);
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to write the session of {Metadata.Name}!");
                Logger.LogDetailed(e);
            }
        }

        /// <summary>
        /// Deserialize the mod session from its raw bytes, fed with data from ReadSession either immediately or async.
        /// </summary>
        public virtual void DeserializeSession(int index, byte[] data) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SessionType == null)
                return;

            _Session = (EverestModuleSession) SessionType.GetConstructor(Everest._EmptyTypeArray).Invoke(Everest._EmptyObjectArray);
            _Session.Index = index;

            if (data == null)
                return;

            try {
                using (MemoryStream stream = new MemoryStream(data)) {
                    if (_Session is EverestModuleBinarySession bs) {
                        using (BinaryReader reader = new BinaryReader(stream))
                            bs.Read(reader);
                    } else {
                        using (StreamReader reader = new StreamReader(stream))
                            YamlHelper.DeserializerUsing(_Session).Deserialize(reader, SessionType);
                    }
                }
                _SaveData.Index = index;
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to deserialize the session of {Metadata.Name}!");
                Logger.LogDetailed(e);
            }
        }

        /// <summary>
        /// Serialize the mod session into its raw bytes, to be fed into WriteSession immediately or async.
        /// </summary>
        public virtual byte[] SerializeSession(int index) {
            if (!SaveDataAsync && !ForceSaveDataAsync)
                throw new Exception($"{Metadata.Name} overrides old methods or otherwise disabled async save data support.");

            if (SessionType == null)
                return null;

            try {
                using (MemoryStream stream = new MemoryStream()) {
                    if (_Session is EverestModuleBinarySession bs) {
                        using (BinaryWriter writer = new BinaryWriter(new UndisposableStream(stream)))
                            bs.Write(writer);
                    } else {
                        using (StreamWriter writer = new StreamWriter(new UndisposableStream(stream)))
                            YamlHelper.Serializer.Serialize(writer, _Session, SessionType);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                    return stream.ToArray();
                }

            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "EverestModule", $"Failed to serialize the session of {Metadata.Name}!");
                Logger.LogDetailed(e);
                return null;
            }
        }

        /// <summary>
        /// Perform any initializing actions after all mods have been loaded.
        /// Do not depend on any specific order in which the mods get initialized.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Perform any initializing actions after Celeste.Initialize has been called.
        /// Do not depend on any specific order in which the mods get initialized.
        /// </summary>
        public virtual void Initialize() {
        }

        /// <summary>
        /// Perform any content loading actions after Celeste.LoadContent has been called.
        /// </summary>
        [Obsolete("Override LoadContent(bool firstLoad) instead.")]
        public virtual void LoadContent() {
        }

        /// <summary>
        /// Perform any content loading actions after Celeste.LoadContent has been called.
        /// </summary>
        /// <param name="firstLoad">Is this the first load?</param>
        public virtual void LoadContent(bool firstLoad) {
#pragma warning disable CS0618 // Type or member is obsolete
            LoadContent();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Unload any unmanaged resources allocated by the mod (f.e. textures) and
        /// undo any changes performed by the mod.
        /// </summary>
        public abstract void Unload();

        /// <summary>
        /// Parse the current command-line argument and any follow-ups.
        /// </summary>
        /// <param name="arg">The current command line argument.</param>
        /// <param name="args">Any further arguments the mod may want to dequeue and parse.</param>
        /// <returns>True if the argument "belongs" to the mod, false otherwise.</returns>
        public virtual bool ParseArg(string arg, Queue<string> args) {
            return false;
        }

        public virtual void OnInputInitialize() {
            if (SettingsType == null)
                return;

            object settings = _Settings;
            if (settings == null)
                return;

            foreach (PropertyInfo prop in SettingsType.GetProperties()) {
                if (!prop.CanRead)
                    continue;

                if (typeof(ButtonBinding).IsAssignableFrom(prop.PropertyType)) {
                    InitializeButtonBinding(settings, prop);

                } else if (false) {
                    // TODO: JoystickBindings
                }
            }
        }

        [MonoModIgnore]
        private extern void InitializeButtonBinding(object settings, PropertyInfo prop);

        [MonoModIfFlag("V1:Input")]
        [MonoModPatch("InitializeButtonBinding")]
        [MonoModReplace]
        [Obsolete]
        private void InitializeButtonBindingV1(object settings, PropertyInfo prop) {
            if (!(prop.GetValue(settings) is ButtonBinding binding)) {
                binding = new ButtonBinding();

                DefaultButtonBindingAttribute defaults = prop.GetCustomAttribute<DefaultButtonBindingAttribute>();
                if (defaults != null) {
                    if (defaults.Button != 0)
                        binding.Buttons.Add(defaults.Button);
                    if (defaults.Key != 0)
                        binding.Keys.Add(defaults.Key);
                }

                prop.SetValue(settings, binding);
            }

            patch_VirtualButton_InputV1 vbutton = new patch_VirtualButton_InputV1();

            foreach (Keys key in binding.Keys)
                vbutton.Nodes.Add(new patch_VirtualButton_InputV1.KeyboardKey(key));

            foreach (Buttons button_ in binding.Buttons) {
                Buttons button = button_;
                if ((button & Buttons.LeftTrigger) == Buttons.LeftTrigger) {
                    vbutton.Nodes.Add(new patch_VirtualButton_InputV1.PadLeftTrigger(Input.Gamepad, 0.25f));
                    button &= ~Buttons.LeftTrigger;
                }
                if ((button & Buttons.RightTrigger) == Buttons.RightTrigger) {
                    vbutton.Nodes.Add(new patch_VirtualButton_InputV1.PadLeftTrigger(Input.Gamepad, 0.25f));
                    button &= ~Buttons.RightTrigger;
                }
                if (button != 0) {
                    vbutton.Nodes.Add(new patch_VirtualButton_InputV1.PadButton(Input.Gamepad, button));
                }
            }

            binding.Button = vbutton;
        }

        [MonoModIfFlag("V2:Input")]
        [MonoModPatch("InitializeButtonBinding")]
        [MonoModReplace]
        private void InitializeButtonBindingV2(object settings, PropertyInfo prop) {
            if (!(prop.GetValue(settings) is ButtonBinding binding)) {
                binding = new ButtonBinding();

                DefaultButtonBindingAttribute defaults = prop.GetCustomAttribute<DefaultButtonBindingAttribute>();
                if (defaults != null) {
                    if (defaults.Button != 0)
                        binding.Binding.Add(defaults.Button);
                    if (defaults.Key != 0)
                        binding.Binding.Add(defaults.Key);
                }

                prop.SetValue(settings, binding);
            }

            binding.Button = (patch_VirtualButton_InputV1) new VirtualButton(binding.Binding, Input.Gamepad, 0.08f, 0.2f);
            ((patch_VirtualButton_InputV2) (VirtualButton) binding.Button).AutoConsumeBuffer = true;
        }

        public virtual void OnInputDeregister() {
            if (SettingsType == null)
                return;

            object settings = _Settings;
            foreach (PropertyInfo prop in SettingsType.GetProperties()) {
                if (!prop.CanRead)
                    continue;

                if (typeof(ButtonBinding).IsAssignableFrom(prop.PropertyType)) {
                    if (!(prop.GetValue(settings) is ButtonBinding binding))
                        continue;

                    binding.Button?.Deregister();

                } else if (false) {
                    // TODO: JoystickBindings
                }
            }
        }

        protected virtual void CreateModMenuSectionHeader(TextMenu menu, bool inGame, EventInstance snapshot) {
            Type type = SettingsType;
            EverestModuleSettings settings = _Settings;
            if (type == null || settings == null)
                return;

            string typeName = type.Name.ToLowerInvariant();
            if (typeName.EndsWith("settings"))
                typeName = typeName.Substring(0, typeName.Length - 8);
            string nameDefaultPrefix = $"modoptions_{typeName}_";

            string name; // We lazily reuse this field for the props later on.
            name = type.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}title";
            name = name.DialogCleanOrNull() ?? Metadata.Name.SpacedPascalCase();

            menu.Add(new patch_TextMenu.patch_SubHeader(name + " | v." + Metadata.VersionString));
        }

        protected virtual void CreateModMenuSectionKeyBindings(TextMenu menu, bool inGame, EventInstance snapshot) {
            menu.Add(new TextMenu.Button(Dialog.Clean("options_keyconfig")).Pressed(() => {
                menu.Focused = false;
                Engine.Scene.Add(CreateKeyboardConfigUI(menu));
                Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
            }));

            menu.Add(new TextMenu.Button(Dialog.Clean("options_btnconfig")).Pressed(() => {
                menu.Focused = false;
                Engine.Scene.Add(CreateButtonConfigUI(menu));
                Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
            }));
        }

        [MonoModIgnore]
        private extern Entity CreateKeyboardConfigUI(TextMenu menu);

        [MonoModIfFlag("V1:Input")]
        [MonoModPatch("CreateKeyboardConfigUI")]
        [MonoModReplace]
        [Obsolete]
        private Entity CreateKeyboardConfigUIV1(TextMenu menu) {
            return new ModuleSettingsKeyboardConfigUI(this) {
                OnClose = () => menu.Focused = true
            };
        }

        [MonoModIfFlag("V2:Input")]
        [MonoModPatch("CreateKeyboardConfigUI")]
        [MonoModReplace]
        private Entity CreateKeyboardConfigUIV2(TextMenu menu) {
            return new ModuleSettingsKeyboardConfigUIV2(this) {
                OnClose = () => menu.Focused = true
            };
        }

        [MonoModIgnore]
        private extern Entity CreateButtonConfigUI(TextMenu menu);

        [MonoModIfFlag("V1:Input")]
        [MonoModPatch("CreateButtonConfigUI")]
        [MonoModReplace]
        [Obsolete]
        private Entity CreateButtonConfigUIV1(TextMenu menu) {
            return new ModuleSettingsButtonConfigUI(this) {
                OnClose = () => menu.Focused = true
            };
        }

        [MonoModIfFlag("V2:Input")]
        [MonoModPatch("CreateButtonConfigUI")]
        [MonoModReplace]
        private Entity CreateButtonConfigUIV2(TextMenu menu) {
            return new ModuleSettingsButtonConfigUIV2(this) {
                OnClose = () => menu.Focused = true
            };
        }

        private Type _PrevSettingsType;
        private PropertyInfo[] _PrevSettingsProps;
        /// <summary>
        /// Create the mod menu subsection including the section header in the given menu.
        /// The default implementation uses reflection to attempt creating a menu.
        /// </summary>
        /// <param name="menu">Menu to add the section to.</param>
        /// <param name="inGame">Whether we're in-game (paused) or in the main menu.</param>
        /// <param name="snapshot">The Level.PauseSnapshot</param>
        public virtual void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            Type type = SettingsType;
            EverestModuleSettings settings = _Settings;
            if (type == null || settings == null)
                return;

            // The default name prefix.
            string typeName = type.Name.ToLowerInvariant();
            if (typeName.EndsWith("settings"))
                typeName = typeName.Substring(0, typeName.Length - 8);
            string nameDefaultPrefix = $"modoptions_{typeName}_";

            // Any attributes we may want to get and read from later.
            SettingInGameAttribute attribInGame;
            SettingRangeAttribute attribRange;
            SettingNumberInputAttribute attribNumber;

            // If the settings type has got the InGame attrib, only show it in the matching situation.
            if ((attribInGame = type.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                attribInGame.InGame != inGame)
                return;

            bool headerCreated = false;
            if (GetType().GetMethod("CreateModMenuSection").DeclaringType != typeof(EverestModule)) {
                CreateModMenuSectionHeader(menu, inGame, snapshot);
                headerCreated = true;
            }

            PropertyInfo[] props;
            if (type == _PrevSettingsType) {
                props = _PrevSettingsProps;
            } else {
                _PrevSettingsProps = props = type.GetProperties();
                _PrevSettingsType = type;
            }

            foreach (PropertyInfo prop in props) {
                MethodInfo creator = type.GetMethod(
                    $"Create{prop.Name}Entry",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(TextMenu), typeof(bool) },
                    new ParameterModifier[0]
                );

                if (creator != null) {
                    if (!headerCreated) {
                        CreateModMenuSectionHeader(menu, inGame, snapshot);
                        headerCreated = true;
                    }

                    creator.GetFastDelegate()(settings, menu, inGame);
                    continue;
                }

                if ((attribInGame = prop.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                    attribInGame.InGame != inGame)
                    continue;

                if (prop.GetCustomAttribute<SettingIgnoreAttribute>() != null)
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                string name = prop.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}";
                name = name.DialogCleanOrNull() ?? prop.Name.SpacedPascalCase();

                bool needsRelaunch = prop.GetCustomAttribute<SettingNeedsRelaunchAttribute>() != null;

                string description = prop.GetCustomAttribute<SettingSubTextAttribute>()?.Description;

                TextMenu.Item item = null;
                Type propType = prop.PropertyType;
                object value = prop.GetValue(settings);

                // Create the matching item based off of the type and attributes.

                if (propType == typeof(bool)) {
                    item =
                        new TextMenu.OnOff(name, (bool) value)
                        .Change(v => prop.SetValue(settings, v))
                    ;

                } else if (
                    propType == typeof(int) &&
                    (attribRange = prop.GetCustomAttribute<SettingRangeAttribute>()) != null
                ) {

                    if (attribRange.LargeRange) {
                        item =
                            new TextMenuExt.IntSlider(name, attribRange.Min, attribRange.Max, (int) value)
                            .Change(v => prop.SetValue(settings, v))
                        ;
                    } else {
                        item =
                            new TextMenu.Slider(name, i => i.ToString(), attribRange.Min, attribRange.Max, (int) value)
                            .Change(v => prop.SetValue(settings, v))
                        ;
                    }

                } else if ((propType == typeof(int) || propType == typeof(float)) &&
                    (attribNumber = prop.GetCustomAttribute<SettingNumberInputAttribute>()) != null) {

                    float currentValue;
                    Action<float> valueSetter;
                    if (propType == typeof(int)) {
                        currentValue = (int) value;
                        valueSetter = v => prop.SetValue(settings, (int) v);
                    } else {
                        currentValue = (float) value;
                        valueSetter = v => prop.SetValue(settings, v);
                    }
                    int maxLength = attribNumber.MaxLength;
                    bool allowNegatives = attribNumber.AllowNegatives;

                    item =
                        new TextMenu.Button(name + ": " + currentValue.ToString($"F{maxLength}").TrimEnd('0').TrimEnd('.'))
                        .Pressed(() => {
                            Audio.Play(SFX.ui_main_savefile_rename_start);
                            menu.SceneAs<Overworld>().Goto<OuiNumberEntry>().Init<OuiModOptions>(
                                currentValue,
                                valueSetter,
                                maxLength,
                                propType == typeof(float),
                                allowNegatives
                            );
                        })
                    ;
                } else if (propType.IsEnum) {
                    Array enumValues = Enum.GetValues(propType);
                    Array.Sort((int[]) enumValues);
                    string enumNamePrefix = $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}_";
                    item =
                        new TextMenu.Slider(name, (i) => {
                            string enumName = enumValues.GetValue(i).ToString();
                            return
                                $"{enumNamePrefix}{enumName.ToLowerInvariant()}".DialogCleanOrNull() ??
                                $"modoptions_{propType.Name.ToLowerInvariant()}_{enumName.ToLowerInvariant()}".DialogCleanOrNull() ??
                                enumName;
                        }, 0, enumValues.Length - 1, (int) value)
                        .Change(v => prop.SetValue(settings, v))
                    ;

                } else if (!inGame && propType == typeof(string)) {
                    int maxValueLength = prop.GetCustomAttribute<SettingMaxLengthAttribute>()?.Max ?? 12;
                    int minValueLength = prop.GetCustomAttribute<SettingMinLengthAttribute>()?.Min ?? 1;

                    item =
                        new TextMenu.Button(name + ": " + value)
                        .Pressed(() => {
                            Audio.Play(SFX.ui_main_savefile_rename_start);
                            menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                                (string) value,
                                v => prop.SetValue(settings, v),
                                maxValueLength,
                                minValueLength
                            );
                        })
                    ;
                }

                if (item == null)
                    continue;

                if (!headerCreated) {
                    CreateModMenuSectionHeader(menu, inGame, snapshot);
                    headerCreated = true;
                }

                menu.Add(item);

                if (needsRelaunch)
                    item = item.NeedsRelaunch(menu);

                if (description != null)
                    item = item.AddDescription(menu, description.DialogCleanOrNull() ?? description);
            }

            foreach (PropertyInfo prop in type.GetProperties()) {
                if ((attribInGame = prop.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                    attribInGame.InGame != inGame)
                    continue;

                if (prop.GetCustomAttribute<SettingIgnoreAttribute>() != null)
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                if (!typeof(ButtonBinding).IsAssignableFrom(prop.PropertyType))
                    continue;

                if (!headerCreated) {
                    CreateModMenuSectionHeader(menu, inGame, snapshot);
                    headerCreated = true;
                }

                CreateModMenuSectionKeyBindings(menu, inGame, snapshot);
                break;
            }
        }

        /// <summary>
        /// Create and add any map data processors to the given context, if any are needed.
        /// </summary>
        /// <param name="context">The context to add the processors to.</param>
        public virtual void PrepareMapDataProcessors(MapDataFixup context) {
        }

    }
}
