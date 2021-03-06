﻿using domi1819.NanoDB;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace domi1819.NanoDBExplorer
{
    public partial class MainForm : Form
    {
        private int loadedLine;
        private string loadedKey;
        private NanoDBFile dbFile;
        private bool editMode;

        private string[] args;

        public MainForm(string[] args)
        {
            this.InitializeComponent();

            this.args = args;

            this.Resize += this.HandleFormSizeChanged;
        }

        internal void GridEditEnterKeyDown()
        {
            if (this.uiCreateButton.Enabled)
            {
                this.uiDbGridView.Focus();
                this.HandleCreateButtonClicked(null, null);
                this.uiGridEdit.Focus();
            }
            else if (this.uiSaveButton.Enabled)
            {
                this.uiDbGridView.Focus();
                this.HandleSaveButtonClicked(null, null);
                this.uiGridEdit.Focus();
            }
        }

        internal void GridEditUpKeyDown()
        {
            DataGridViewSelectedCellCollection cells = this.uiGridEdit.SelectedCells;

            if (cells.Count > 0 && this.dbFile.Layout.Elements[cells[0].ColumnIndex] is DataBlobElement)
            {
                this.uiDbGridView.Focus();
                this.uiGridEdit.Focus();

                BlobEditor editor = new BlobEditor((string)cells[0].Value);

                cells[0].Value = editor.GetResult(this);
                editor.Dispose();

                this.uiDbGridView.Focus();
                this.uiGridEdit.Focus();
            }
        }

        private void HandleOpenToolStripMenuItemClick(object sender, EventArgs e)
        {
            this.openFileDialog.FileName = "";

            if (this.openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                this.OpenFile(this.openFileDialog.FileName);
            }
        }

        private void OpenFile(string path)
        {
            this.editMode = false;

            if (this.dbFile != null)
            {
                this.dbFile.Unbind();
            }

            this.dbFile = new NanoDBFile(path);

            if (this.dbFile.Initialize() == InitializeResult.Success)
            {
                this.dbFile.Load(this.dbFile.RecommendedIndex);

                this.dbFile.Bind();

                this.uiDbGridView.Rows.Clear();
                this.uiGridEdit.Rows.Clear();

                this.uiDbGridView.ColumnCount = this.dbFile.Layout.LayoutSize;
                this.uiGridEdit.ColumnCount = this.dbFile.Layout.LayoutSize;

                for (int i = 0; i < this.dbFile.Layout.LayoutSize; i++)
                {
                    if (i == this.dbFile.RecommendedIndex)
                    {
                        this.uiDbGridView.Columns[i].Name = ">> " + this.dbFile.Layout.Elements[i].GetName();
                    }
                    else
                    {
                        this.uiDbGridView.Columns[i].Name = this.dbFile.Layout.Elements[i].GetName();
                    }

                    this.uiDbGridView.Columns[i].Width = 150;
                }

                foreach (string key in this.dbFile.GetAllKeys())
                {
                    this.uiDbGridView.Rows.Add(this.Serialize(this.dbFile.GetLine(key)));
                }

                this.uiGridEdit.Rows.Add();

                this.uiResetButton.Enabled = true;
                this.editMode = true;

                // Workaround to reset the selection when started with a parameter
                new Thread(() => { Thread.Sleep(50); this.Invoke((MethodInvoker)(() => this.HandleResetButtonClicked(null, null))); }).Start();
            }
            else
            {
                this.dbFile = null;
                MessageBox.Show(this, "Database doesn't have a proper format!");
            }
        }

        private void HandleNewToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (this.dbFile != null)
            {
                this.dbFile.Unbind();
            }

            this.saveFileDialog.FileName = "";

            if (this.saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.editMode = false;

                this.dbFile = new NanoDBFile(this.saveFileDialog.FileName);

                this.dbFile.Initialize();

                List<NanoDBElement> list = new List<NanoDBElement>();

                int curLayoutId = 0;

                while (true)
                {
                    ElementDialog dialog = new ElementDialog(curLayoutId);
                    dialog.ShowDialog();


                    list.Add(dialog.ReturnValue);
                    curLayoutId++;

                    if (dialog.Finish || curLayoutId == 256)
                    {
                        break;
                    }
                }

                this.dbFile.CreateNew(new NanoDBLayout(list.ToArray()), 0);

                this.dbFile.Bind();

                this.uiDbGridView.Rows.Clear();
                this.uiGridEdit.Rows.Clear();

                this.uiDbGridView.ColumnCount = this.dbFile.Layout.LayoutSize;
                this.uiGridEdit.ColumnCount = this.dbFile.Layout.LayoutSize;

                for (int i = 0; i < this.dbFile.Layout.LayoutSize; i++)
                {
                    if (i == this.dbFile.RecommendedIndex)
                    {
                        this.uiDbGridView.Columns[i].Name = ">> " + this.dbFile.Layout.Elements[i].GetName();
                    }
                    else
                    {
                        this.uiDbGridView.Columns[i].Name = this.dbFile.Layout.Elements[i].GetName();
                    }

                    this.uiDbGridView.Columns[i].Width = 150;
                }

                this.uiGridEdit.Rows.Add();

                this.uiResetButton.Enabled = true;

                this.HandleResetButtonClicked(null, null);

                this.editMode = true;
            }
        }

        private void HandleExitToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (this.dbFile != null)
            {
                this.dbFile.Unbind();
            }

            Application.Exit();
        }

        private void HandleDbCleanerToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (this.dbFile != null)
            {
                DbCleaner cleaner = new DbCleaner(this.dbFile);

                cleaner.ShowDialog(this);

                if (cleaner.ReopenPath != null)
                {
                    this.OpenFile(cleaner.ReopenPath);
                }
            }
            else
            {
                MessageBox.Show("Please open a database file first!");
            }
        }

        private void HandleCreateButtonClicked(object sender, EventArgs e)
        {
            object[] dbObjects = new object[this.uiGridEdit.Columns.Count];
            string key = "";

            for (int i = 0; i < this.uiGridEdit.Columns.Count; i++)
            {
                dbObjects[i] = this.dbFile.Layout.Elements[i].Deserialize((string)this.uiGridEdit.Rows[0].Cells[i].Value);

                if (i == this.dbFile.RecommendedIndex)
                {
                    key = (string)dbObjects[i];
                }
            }

            if (!string.IsNullOrEmpty(key) && !this.dbFile.ContainsKey(key))
            {
                if (this.dbFile.AddLine(dbObjects) != null)
                {
                    this.uiDbGridView.Rows.Add(this.Serialize(this.dbFile.GetLine(key)));

                    this.HandleResetButtonClicked(sender, null);
                }
                else
                {
                    MessageBox.Show(this, "Could not add line to database!");
                }
            }
            else if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show(this, "Invalid key!");
            }
            else
            {
                MessageBox.Show(this, "Key " + key + " already exists!");
            }
        }

        private void HandleSaveButtonClicked(object sender, EventArgs e)
        {
            object[] objects = new object[this.uiGridEdit.Columns.Count];
            string key = "";

            for (int i = 0; i < this.uiGridEdit.Columns.Count; i++)
            {
                objects[i] = this.dbFile.Layout.Elements[i].Deserialize(this.uiGridEdit.Rows[0].Cells[i].Value.ToString());

                if (i == this.dbFile.RecommendedIndex)
                {
                    key = (string)objects[i];
                }
            }

            if (key == this.loadedKey || (!string.IsNullOrEmpty(key) && !this.dbFile.ContainsKey(key)))
            {
                this.dbFile.GetLine(this.loadedKey).SetValues(objects);

                this.uiDbGridView.Rows[this.loadedLine].SetValues(this.Serialize(this.dbFile.GetLine(key)));

                this.HandleResetButtonClicked(sender, null);
            }
            else if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show(this, "Invalid key!");
            }
            else
            {
                MessageBox.Show(this, "Key " + key + " already exists!");
            }
        }

        private void HandleResetButtonClicked(object sender, EventArgs e)
        {
            this.loadedLine = 0;
            this.loadedKey = null;

            this.uiDbGridView.ClearSelection();

            this.uiGridEdit.Rows.RemoveAt(0);
            this.uiGridEdit.Rows.Add();

            this.uiCreateButton.Enabled = true;
            this.uiSaveButton.Enabled = false;
            this.uiDeleteButton.Enabled = false;
        }

        private void HandleDeleteButtonClicked(object sender, EventArgs e)
        {
            this.dbFile.GetLine(this.loadedKey).Remove();
            this.uiDbGridView.Rows.RemoveAt(this.loadedLine);

            this.HandleResetButtonClicked(sender, null);
        }

        private void HandleFormSizeChanged(object sender, EventArgs args)
        {
            this.uiDbGridView.Size = new Size(this.Size.Width - 14, this.Size.Height - 131);

            this.uiGridEdit.Size = new Size(this.Size.Width - 14, 25);
            this.uiGridEdit.Location = new Point(-1, this.Size.Height - 98);

            this.uiCreateButton.Location = new Point(5, this.Size.Height - 67);
            this.uiSaveButton.Location = new Point(109, this.Size.Height - 67);
            this.uiResetButton.Location = new Point(213, this.Size.Height - 67);
            this.uiDeleteButton.Location = new Point(317, this.Size.Height - 67);
        }

        private void HandleColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            for (int i = 0; i < this.uiDbGridView.Columns.Count; i++)
            {
                this.uiGridEdit.Columns[i].Width = this.uiDbGridView.Columns[i].Width;
            }
        }

        private void HandleScroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
            {
                this.uiGridEdit.HorizontalScrollingOffset = this.uiDbGridView.HorizontalScrollingOffset;
            }
        }

        private void HandleSelectionChanged(object sender, EventArgs e)
        {
            if (this.editMode && this.uiDbGridView.SelectedRows.Count > 0)
            {
                int rowIndex = this.uiDbGridView.SelectedRows[0].Index;

                this.loadedLine = rowIndex;
                this.loadedKey = (string)this.uiDbGridView.Rows[rowIndex].Cells[this.dbFile.RecommendedIndex].Value;

                this.uiGridEdit.Rows.RemoveAt(0);

                NanoDBLine line = this.dbFile.GetLine(this.loadedKey);

                this.uiGridEdit.Rows.Add(this.Serialize(line));

                this.uiCreateButton.Enabled = false;
                this.uiSaveButton.Enabled = true;
                this.uiDeleteButton.Enabled = true;
            }
        }

        private object[] Serialize(NanoDBLine objects)
        {
            object[] retObjects = new object[objects.ElementCount];

            if (objects.ElementCount == this.dbFile.Layout.LayoutSize)
            {
                for (int i = 0; i < objects.ElementCount; i++)
                {
                    retObjects[i] = this.dbFile.Layout.Elements[i].Serialize(objects[i]);
                }
            }

            return retObjects;
        }

        private void HandleDragFile(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 1 && !File.GetAttributes(files[0]).HasFlag(FileAttributes.Directory))
            {
                e.Effect = DragDropEffects.Move;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void HandleDropFile(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            this.OpenFile(files[0]);
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.dbFile != null)
            {
                this.dbFile.Unbind();
            }
        }

        private void HandleFormLoad(object sender, EventArgs e)
        {
            this.Refresh();

            if (this.args.Length > 0 && File.Exists(this.args[0]))
            {
                new Thread(() => { Thread.Sleep(100); this.Invoke((MethodInvoker)(() => this.OpenFile(this.args[0]))); }).Start();
            }
        }
    }
}
