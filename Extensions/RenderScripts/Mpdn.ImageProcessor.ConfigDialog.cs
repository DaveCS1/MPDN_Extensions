// This file is a part of MPDN Extensions.
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Mpdn.Extensions.Framework.Config;
using Mpdn.Extensions.Framework.Controls;
using Mpdn.Extensions.Framework.RenderChain;

namespace Mpdn.Extensions.RenderScripts
{
    namespace Mpdn.ImageProcessor
    {
        public partial class ImageProcessorConfigDialog : ImageProcessorConfigDialogBase
        {
            private string m_ShaderPath;

            public ImageProcessorConfigDialog()
            {
                InitializeComponent();
                UpdateButtons();
            }

            protected override void LoadSettings()
            {
                m_ShaderPath = Settings.FullShaderPath;

                Add(Settings.ShaderFileNames);

                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }

                checkBoxCompatibilityMode.Checked = Settings.CompatibilityMode;
                checkBoxYUVProcessing.Checked = Settings.ProcessInYUV;
            }

            protected override void SaveSettings()
            {
                Settings.ShaderFileNames = listBox.Items.Cast<string>().ToArray();
                Settings.CompatibilityMode = checkBoxCompatibilityMode.Checked;
                Settings.ProcessInYUV = checkBoxYUVProcessing.Checked;
            }

            private void ButtonAddClick(object sender, EventArgs e)
            {
                openFileDialog.InitialDirectory = m_ShaderPath;
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                Add(openFileDialog.FileNames);
                UpdateButtons();
            }

            private void ButtonRemoveClick(object sender, EventArgs e)
            {
                RemoveItem();
                UpdateButtons();
            }

            private void ButtonClearClick(object sender, EventArgs e)
            {
                listBox.Items.Clear();
                UpdateButtons();
            }

            private void ButtonUpClick(object sender, EventArgs e)
            {
                MoveItem((int) Direction.Up);
                UpdateButtons();
            }

            private void ButtonDownClick(object sender, EventArgs e)
            {
                MoveItem((int) Direction.Down);
                UpdateButtons();
            }

            private void RemoveItem()
            {
                var index = listBox.SelectedIndex;
                listBox.Items.RemoveAt(index);
                listBox.SelectedIndex = index < listBox.Items.Count ? index : listBox.Items.Count - 1;
            }

            private void MoveItem(int direction)
            {
                var index = listBox.SelectedIndex;
                var item = listBox.Items[index];
                listBox.Items.RemoveAt(index);
                listBox.Items.Insert(index + direction, item);
                listBox.SelectedIndex = index + direction;
            }

            private void Add(IEnumerable<string> fileNames)
            {
                foreach (var fileName in fileNames)
                {
                    listBox.Items.Add(ShaderCache.GetRelativePath(m_ShaderPath, fileName));
                }
            }

            private void ListBoxSelectedIndexChanged(object sender, EventArgs e)
            {
                UpdateButtons();
            }

            private void UpdateButtons()
            {
                var index = listBox.SelectedIndex;
                var count = listBox.Items.Count;

                buttonEdit.Enabled = index >= 0;
                buttonRemove.Enabled = index >= 0;
                buttonUp.Enabled = index > 0;
                buttonDown.Enabled = index >= 0 && index < count - 1;
                buttonClear.Enabled = count > 0;
            }

            private void ButtonEditClick(object sender, EventArgs e)
            {
                EditSelectedFile();
            }

            private void EditSelectedFile()
            {
                var file = Path.Combine(m_ShaderPath, (string) listBox.SelectedItem);
                if (!File.Exists(file))
                {
                    MessageBox.Show(this, string.Format("File not found '{0}'", file), "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                using (new HourGlass())
                {
                    var editor = new ScriptedRenderChainScriptEditorDialog();
                    editor.LoadFile(file);
                    editor.ShowDialog(this);
                }
            }

            private void ListBoxMouseDoubleClick(object sender, MouseEventArgs e)
            {
                if (!buttonEdit.Enabled)
                    return;

                EditSelectedFile();
            }

            private enum Direction
            {
                Up = -1,
                Down = 1
            }
        }

        public class ImageProcessorConfigDialogBase : ScriptConfigDialog<ImageProcessor>
        {
        }
    }
}
