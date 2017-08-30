﻿// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.
// 

using System;
using System.Windows.Forms;
using YAXLib;

namespace Mpdn.Extensions.Framework.Chain
{
    public interface INameable
    {
        string Name { set; }
    }

    public class Preset<T, TScript> : Chain<T>, IChainUi<T, TScript>, INameable
        where TScript : class, IScript
    {
        #region Settings

        private string m_Name;
        private IChainUi<T, TScript> m_Script;

        [YAXAttributeForClass]
        public string Name
        {
            get { return m_Name; }
            set
            {
                m_Name = value;
                var chain = Chain as INameable;
                if (chain != null)
                    chain.Name = value;
            }
        }

        public IChainUi<T, TScript> Script
        {
            get { return m_Script ?? ChainUi<T, TScript>.IDENTITY; }
            set
            {
                m_Script = value;
                var chain = Chain as INameable;
                if (chain != null)
                    chain.Name = Name;
            }
        }

        #endregion

        #region Chain implementation

        public override T Process(T input)
        {
            return Script != null ? input + Chain : input;
        }

        #endregion

        #region ChainUi Implementation

        [YAXDontSerialize]
        public string Description
        {
            get { return Script.Descriptor.Description; }
        }

        [YAXDontSerialize]
        public Chain<T> Chain
        {
            get { return Script.Chain; }
        }

        [YAXDontSerialize]
        public string Category
        {
            get { return Script.Category; }
        }

        [YAXDontSerialize]
        public int Version
        {
            get { return Script.Version; }
        }

        [YAXDontSerialize]
        public ExtensionUiDescriptor Descriptor
        {
            get { return Script.Descriptor; }
        }

        public bool HasConfigDialog()
        {
            return Script.HasConfigDialog();
        }

        public bool ShowConfigDialog(IWin32Window owner)
        {
            return Script.ShowConfigDialog(owner);
        }

        public TScript CreateScript()
        {
            return Script.CreateScript();
        }

        void IExtensionUi.Initialize()
        {
            Script.Initialize();
        }

        public void Destroy()
        {
            Script.Destroy();
        }

        #endregion

        public override string ToString()
        {
            return Name;
        }
    }
}