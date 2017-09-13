﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Eto;
using Eto.Forms;
using Eto.GtkSharp.Forms;
using Eto.GtkSharp.Forms.Controls;
using Eto.GtkSharp.Forms.ToolBar;

namespace MonoGame.Tools.Pipeline
{
    static partial class Gtk3Wrapper
    {
        public const string gtklibpath = "libgtk-3.so.0";

        [DllImport(gtklibpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr gtk_header_bar_new();

        [DllImport(gtklibpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gtk_window_set_titlebar(IntPtr window, IntPtr widget);

        [DllImport(gtklibpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gtk_header_bar_pack_start(IntPtr bar, IntPtr child);

        [DllImport(gtklibpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gtk_header_bar_pack_end(IntPtr bar, IntPtr child);

        [DllImport(gtklibpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gtk_header_bar_set_show_close_button(IntPtr bar, bool setting);

        [DllImport(gtklibpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gtk_header_bar_set_subtitle(IntPtr handle, string text);
    }

    public class ModalButton : Gtk.Button
    {
        [GLib.Property("active")]
        public bool Active
        {
            set
            {
                this.SetProperty("active", new GLib.Value(value));
            }
        }

        public ModalButton(IntPtr handle) : base(handle) { }
    }

    public static class Styles
    {
        private static Gtk.Widget popovermenu1, popovermenu2;

        private static void Connect(IntPtr handle, Command com, bool sensitivity = true)
        {
            var b = new Gtk.Button(handle);
            b.Clicked += delegate
            {
                popovermenu1.Hide();
                popovermenu2.Hide();
                com.Execute();
            };

            com.EnabledChanged += delegate
            {
                if (sensitivity)
                    b.Sensitive = com.Enabled;
                else
                    b.Visible = com.Enabled;
            };
        }

        private static void Connect(IntPtr handle, CheckCommand com)
        {
            var widget = new ModalButton(handle);
            widget.Active = com.Checked;

            widget.Clicked += delegate
            {
                com.Checked = !com.Checked;
                widget.Active = com.Checked;
            };
        }

        [GLib.ConnectBefore]
        public static void TreeView_ButtonPressEvent(object o, Gtk.ButtonPressEventArgs args)
        {
            var treeview = o as Gtk.TreeView;

            if (args.Event.Button == 3)
            {
                Gtk.TreeViewDropPosition pos;
                Gtk.TreePath path;
                Gtk.TreeIter iter;

                if (treeview.GetDestRowAtPos((int)args.Event.X, (int)args.Event.Y, out path, out pos) && treeview.Model.GetIter(out iter, path))
                {
                    var paths = treeview.Selection.GetSelectedRows().ToList();
                    if (paths.Contains(path))
                        args.RetVal = true;
                }
            }
        }

        public static void Load()
        {
            Style.Add<ApplicationHandler>("PipelineTool", h =>
            {
                if (Gtk.Global.MajorVersion >= 3 && Gtk.Global.MinorVersion >= 16)
                    Global.UseHeaderBar = Gtk3Wrapper.gtk_application_prefers_app_menu(h.Control.Handle);
            });

            Style.Add<FormHandler>("MainWindow", h =>
            {
                if (!Global.UseHeaderBar)
                    return;

                h.Menu = null;
                h.ToolBar = null;

                var builder = new Gtk.Builder(null, "MainWindow.glade", null);
                var headerBar = new Gtk.Widget(builder.GetObject("headerbar").Handle);
                var separator = new Gtk.Widget(builder.GetObject("separator1").Handle);

                popovermenu1 = new Gtk.Widget(builder.GetObject("popovermenu1").Handle);
                popovermenu2 = new Gtk.Widget(builder.GetObject("popovermenu2").Handle);

                Gtk3Wrapper.gtk_window_set_titlebar(h.Control.Handle, headerBar.Handle);
                Gtk3Wrapper.gtk_header_bar_set_show_close_button(headerBar.Handle, true);

                Connect(builder.GetObject("new_button").Handle, MainWindow.Instance.cmdNew);
                Connect(builder.GetObject("save_button").Handle, MainWindow.Instance.cmdSave);
                Connect(builder.GetObject("build_button").Handle, MainWindow.Instance.cmdBuild, false);
                Connect(builder.GetObject("rebuild_button").Handle, MainWindow.Instance.cmdRebuild, false);
                Connect(builder.GetObject("cancel_button").Handle, MainWindow.Instance.cmdCancelBuild, false);
                Connect(builder.GetObject("open_other_button").Handle, MainWindow.Instance.cmdOpen);
                Connect(builder.GetObject("import_button").Handle, MainWindow.Instance.cmdImport);
                Connect(builder.GetObject("saveas_button").Handle, MainWindow.Instance.cmdSaveAs);
                Connect(builder.GetObject("undo_button").Handle, MainWindow.Instance.cmdUndo);
                Connect(builder.GetObject("redo_button").Handle, MainWindow.Instance.cmdRedo);
                Connect(builder.GetObject("close_button").Handle, MainWindow.Instance.cmdClose);
                Connect(builder.GetObject("clean_button").Handle, MainWindow.Instance.cmdClean);
                Connect(builder.GetObject("help_button").Handle, MainWindow.Instance.cmdHelp);
                Connect(builder.GetObject("about_button").Handle, MainWindow.Instance.cmdAbout);
                Connect(builder.GetObject("exit_button").Handle, MainWindow.Instance.cmdExit);

                MainWindow.Instance.cmdBuild.EnabledChanged += (sender, e) =>
                    separator.Visible = MainWindow.Instance.cmdBuild.Enabled || MainWindow.Instance.cmdCancelBuild.Enabled;
                MainWindow.Instance.cmdCancelBuild.EnabledChanged += (sender, e) => 
                    separator.Visible = MainWindow.Instance.cmdBuild.Enabled || MainWindow.Instance.cmdCancelBuild.Enabled;

                MainWindow.Instance.TitleChanged += delegate
                {
                    var title = MainWindow.TitleBase;
                    var subtitle = "";

                    if (PipelineController.Instance.ProjectOpen)
                    {
                        title = (PipelineController.Instance.ProjectDirty) ? "*" : "";
                        title += Path.GetFileName(PipelineController.Instance.ProjectItem.OriginalPath);
                        subtitle = Path.GetDirectoryName(PipelineController.Instance.ProjectItem.OriginalPath);
                    }

                    h.Control.Title = title;
                    Gtk3Wrapper.gtk_header_bar_set_subtitle(headerBar.Handle, subtitle);
                };

                var treeview1 = new Gtk.TreeView(builder.GetObject("treeview1").Handle);
                var store = new Gtk.TreeStore(typeof(string), typeof(string));
                var column = new Gtk.TreeViewColumn();
                var textCell = new Gtk.CellRendererText();
                var dataCell = new Gtk.CellRendererText();
                dataCell.Visible = false;
                column.PackStart(textCell, false);
                column.PackStart(dataCell, false);
                treeview1.AppendColumn(column);
                column.AddAttribute(textCell, "markup", 0);
                column.AddAttribute(dataCell, "text", 1);
                treeview1.Model = store;

                MainWindow.Instance.RecentChanged += (sender, e) =>
                {
                    store.Clear();
                    var recentList = sender as List<string>;

                    foreach (var project in recentList)
                        store.InsertWithValues(0, "<b>" + Path.GetFileName(project) + "</b>\n" +
                                               Path.GetDirectoryName(project), project);
                };

                treeview1.RowActivated += (o, args) =>
                {
                    popovermenu2.Hide();

                    Gtk.TreeIter iter;
                    if (!store.GetIter(out iter, args.Path))
                        return;

                    var project = store.GetValue(iter, 1).ToString();
                    PipelineController.Instance.OpenProject(project);
                };

                h.Control.ShowAll();
            });

            Style.Add<ButtonHandler>("Destuctive", h => h.Control.StyleContext.AddClass("destructive-action"));

            Style.Add<LabelHandler>("Wrap", h => h.Control.MaxWidthChars = 55);

            Style.Add<ToolBarHandler>("ToolBar", h =>
            {
                h.Control.ToolbarStyle = Gtk.ToolbarStyle.Icons;
                h.Control.IconSize = Gtk.IconSize.SmallToolbar;
            });

            Style.Add<DrawableHandler>("Stretch", h =>
            {
                var parent = h.Control.Parent.Parent.Parent.Parent.Parent.Parent;

                parent.SizeAllocated += delegate
                {
                    var al = h.Control.Allocation;
                    al.Width = parent.AllocatedWidth - 2;
                    h.Control.SetAllocation(al);
                };
            });

            Style.Add<PixelLayoutHandler>("Stretch", h =>
            {
                var parent = h.Control.Parent.Parent.Parent.Parent.Parent;

                parent.SizeAllocated += delegate
                {
                     var al = h.Control.Allocation;
                     al.Width = parent.AllocatedWidth;
                     h.Control.SetAllocation(al);
                };
            });
        }
    }
}

