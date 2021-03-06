﻿using System;
using System.IO;
using System.Xml.Linq;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.IO;
using TwitchLeecher.Shared.Reflection;

namespace TwitchLeecher.Services.Services
{
    internal class RuntimeDataService : IRuntimeDataService
    {
        #region Constants

        private const string RUNTIMEDATA_FILE = "runtime.xml";

        private const string RUNTIMEDATA_EL = "RuntimeData";
        private const string RUNTIMEDATA_VERSION_ATTR = "Version";

        private const string AUTH_EL = "Authorization";
        private const string AUTH_ACCESSTOKEN_EL = "AccessToken";

        private const string APP_EL = "Application";

        #endregion Constants

        #region Fields

        private IFolderService folderService;

        private RuntimeData runtimeData;
        private Version tlVersion;

        private readonly object commandLockObject;

        #endregion Fields

        #region Constructors

        public RuntimeDataService(IFolderService folderService)
        {
            this.folderService = folderService;
            this.tlVersion = AssemblyUtil.Get.GetAssemblyVersion().Trim();
            this.commandLockObject = new object();
        }

        #endregion Constructors

        #region Properties

        public RuntimeData RuntimeData
        {
            get
            {
                if (this.runtimeData == null)
                {
                    this.runtimeData = this.Load();
                }

                return this.runtimeData;
            }
        }

        #endregion Properties

        #region Methods

        public void Save()
        {
            lock (this.commandLockObject)
            {
                RuntimeData runtimeData = this.RuntimeData;

                XDocument doc = new XDocument(new XDeclaration("1.0", "UTF-8", null));

                XElement runtimeDataEl = new XElement(RUNTIMEDATA_EL);
                runtimeDataEl.Add(new XAttribute(RUNTIMEDATA_VERSION_ATTR, this.tlVersion));
                doc.Add(runtimeDataEl);

                if (!string.IsNullOrWhiteSpace(runtimeData.AccessToken))
                {
                    XElement authEl = new XElement(AUTH_EL);
                    runtimeDataEl.Add(authEl);

                    XElement accessTokenEl = new XElement(AUTH_ACCESSTOKEN_EL);
                    accessTokenEl.SetValue(runtimeData.AccessToken);
                    authEl.Add(accessTokenEl);
                }

                if (runtimeData.MainWindowInfo != null)
                {
                    XElement mainWindowInfoEl = runtimeData.MainWindowInfo.GetXml();

                    if (mainWindowInfoEl.HasElements)
                    {
                        XElement applicationEl = new XElement(APP_EL);
                        applicationEl.Add(mainWindowInfoEl);
                        runtimeDataEl.Add(applicationEl);
                    }
                }

                string appDataFolder = this.folderService.GetAppDataFolder();

                FileSystem.CreateDirectory(appDataFolder);

                string configFile = Path.Combine(appDataFolder, RUNTIMEDATA_FILE);

                doc.Save(configFile);
            }
        }

        private RuntimeData Load()
        {
            lock (this.commandLockObject)
            {
                string configFile = Path.Combine(this.folderService.GetAppDataFolder(), RUNTIMEDATA_FILE);

                RuntimeData runtimeData = new RuntimeData()
                {
                    Version = this.tlVersion
                };

                if (File.Exists(configFile))
                {
                    XDocument doc = XDocument.Load(configFile);

                    XElement runtimeDataEl = doc.Root;

                    if (runtimeDataEl != null)
                    {
                        XAttribute rtVersionAttr = runtimeDataEl.Attribute(RUNTIMEDATA_VERSION_ATTR);

                        Version rtVersion = null;

                        if (rtVersionAttr != null && Version.TryParse(rtVersionAttr.Value, out rtVersion))
                        {
                            runtimeData.Version = rtVersion;
                        }
                        else
                        {
                            runtimeData.Version = new Version(1, 0);
                        }

                        XElement authEl = runtimeDataEl.Element(AUTH_EL);

                        if (authEl != null)
                        {
                            XElement accessTokenEl = authEl.Element(AUTH_ACCESSTOKEN_EL);

                            if (accessTokenEl != null)
                            {
                                try
                                {
                                    runtimeData.AccessToken = accessTokenEl.GetValueAsString();
                                }
                                catch
                                {
                                    // Value from config file could not be loaded, use default value
                                }
                            }
                        }

                        XElement applicationEl = runtimeDataEl.Element(APP_EL);

                        if (applicationEl != null)
                        {
                            XElement mainWindowInfoEl = applicationEl.Element(MainWindowInfo.MAINWINDOW_EL);

                            if (mainWindowInfoEl != null)
                            {
                                try
                                {
                                    runtimeData.MainWindowInfo = MainWindowInfo.GetFromXml(mainWindowInfoEl);
                                }
                                catch
                                {
                                    // Value from config file could not be loaded, use default value
                                }
                            }
                        }
                    }
                }

                return runtimeData;
            }
        }

        #endregion Methods
    }
}