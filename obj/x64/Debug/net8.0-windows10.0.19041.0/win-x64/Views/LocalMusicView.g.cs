﻿#pragma checksum "C:\Users\blueo\source\repos\BlueMusicPlayer\Views\LocalMusicView.xaml" "{8829d00f-11b8-4213-878b-770e8597ac16}" "6BC159A0322C35A0755FED5943E05EA74F86643A7B1F00FFD64626118A6F59D1"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace BlueMusicPlayer.Views
{
    partial class LocalMusicView : 
        global::Microsoft.UI.Xaml.Controls.UserControl, 
        global::Microsoft.UI.Xaml.Markup.IComponentConnector
    {

        /// <summary>
        /// Connect()
        /// </summary>
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.UI.Xaml.Markup.Compiler"," 3.0.0.2505")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public void Connect(int connectionId, object target)
        {
            switch(connectionId)
            {
            case 1: // Views\LocalMusicView.xaml line 1
                {
                    this.RootControl = global::WinRT.CastExtensions.As<global::Microsoft.UI.Xaml.Controls.UserControl>(target);
                }
                break;
            case 2: // Views\LocalMusicView.xaml line 13
                {
                    this.LayoutRoot = global::WinRT.CastExtensions.As<global::Microsoft.UI.Xaml.Controls.Grid>(target);
                }
                break;
            case 3: // Views\LocalMusicView.xaml line 69
                {
                    this.LocalMusicScrollViewer = global::WinRT.CastExtensions.As<global::Microsoft.UI.Xaml.Controls.ScrollViewer>(target);
                }
                break;
            case 4: // Views\LocalMusicView.xaml line 75
                {
                    this.TracksRepeater = global::WinRT.CastExtensions.As<global::Microsoft.UI.Xaml.Controls.ItemsRepeater>(target);
                }
                break;
            case 7: // Views\LocalMusicView.xaml line 36
                {
                    this.SearchBox = global::WinRT.CastExtensions.As<global::Microsoft.UI.Xaml.Controls.AutoSuggestBox>(target);
                }
                break;
            default:
                break;
            }
            this._contentLoaded = true;
        }


        /// <summary>
        /// GetBindingConnector(int connectionId, object target)
        /// </summary>
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.UI.Xaml.Markup.Compiler"," 3.0.0.2505")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::Microsoft.UI.Xaml.Markup.IComponentConnector GetBindingConnector(int connectionId, object target)
        {
            global::Microsoft.UI.Xaml.Markup.IComponentConnector returnValue = null;
            return returnValue;
        }
    }
}

