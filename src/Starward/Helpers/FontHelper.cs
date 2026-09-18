using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Starward.Features.ViewHost;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Starward.Helpers;

public static class FontHelper
{


    private static readonly ILogger _logger = AppConfig.GetService<ILoggerFactory>().CreateLogger(typeof(FontHelper));


    /// <summary>
    /// 获取系统已安装的字体家族，按当前界面语言的本地化名称排序，每次调用时重新枚举以反映字体安装的变化。
    /// LocalizedName 用于显示，CanonicalName 为 en-us 规范名称，用于保存设置，使已保存的字体不受应用界面语言影响。
    /// </summary>
    public static IReadOnlyList<(string LocalizedName, string CanonicalName)> GetSystemFontFamilies()
    {
        var list = new List<(string LocalizedName, string CanonicalName)>(512);
        try
        {
            var iid = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48"); // IDWriteFactory
            DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, in iid, out object obj);
            var factory = (IDWriteFactory)obj;
            factory.GetSystemFontCollection(out IDWriteFontCollection collection, false);
            uint count = collection.GetFontFamilyCount();
            for (uint i = 0; i < count; i++)
            {
                collection.GetFontFamily(i, out IDWriteFontFamily fontFamily);
                fontFamily.GetFamilyNames(out IDWriteLocalizedStrings familyNames);
                (string LocalizedName, string CanonicalName) names = GetFamilyNames(familyNames);
                if (!string.IsNullOrWhiteSpace(names.LocalizedName))
                {
                    list.Add(names);
                }
            }
            list.Sort((x, y) => StringComparer.CurrentCulture.Compare(x.LocalizedName, y.LocalizedName));
        }
        catch { }
        return list;
    }



    /// <summary>
    /// 应用自定义字体，仅影响界面文字，不影响图标。
    /// 传入 null 或空字符串时恢复默认字体。
    /// </summary>
    public static void ApplyCustomFont(string? fontFamily)
    {
        try
        {
            var resources = Application.Current.Resources;
            if (string.IsNullOrWhiteSpace(fontFamily))
            {
                resources.Remove("ContentControlThemeFontFamily");
            }
            else
            {
                resources["ContentControlThemeFontFamily"] = new FontFamily(fontFamily);
            }
            // 切换两次 RequestedTheme 强制已打开的界面重新求值 ThemeResource，使字体更改实时生效
            if (MainWindow.Current?.Content is FrameworkElement element)
            {
                element.RequestedTheme = element.ActualTheme switch
                {
                    ElementTheme.Light => ElementTheme.Dark,
                    ElementTheme.Dark => ElementTheme.Light,
                    _ => ElementTheme.Default,
                };
                element.RequestedTheme = ElementTheme.Default;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply custom font");
        }
    }



    private static (string LocalizedName, string CanonicalName) GetFamilyNames(IDWriteLocalizedStrings familyNames)
    {
        string localizedName = "", canonicalName = "";
        try
        {
            string ReadByIndex(uint index)
            {
                familyNames.GetStringLength(index, out uint length);
                var sb = new StringBuilder((int)length + 1);
                familyNames.GetString(index, sb, (uint)sb.Capacity);
                return sb.ToString();
            }

            string ReadByLocale(string locale)
            {
                familyNames.FindLocaleName(locale, out uint index, out bool exists);
                return exists ? ReadByIndex(index) : "";
            }

            localizedName = ReadByLocale(CultureInfo.CurrentUICulture.Name);
            if (string.IsNullOrEmpty(localizedName))
            {
                localizedName = ReadByLocale(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            }
            if (string.IsNullOrEmpty(localizedName))
            {
                localizedName = ReadByLocale("en-us");
            }
            if (string.IsNullOrEmpty(localizedName))
            {
                localizedName = ReadByIndex(0);
            }
            canonicalName = ReadByLocale("en-us");
            if (string.IsNullOrEmpty(canonicalName))
            {
                // 个别字体没有 en-us 名称，保存时退回本地化名称
                canonicalName = localizedName;
            }
        }
        catch { }
        return (localizedName, canonicalName);
    }



    #region DirectWrite Interop


    // 接口只声明实际用到的虚表槽位，名称与顺序必须和 dwrite.h 完全一致；
    // IDWriteFontFamily 不能用继承式声明，CLR 对 ComImport 派生接口生成的虚表布局与原生不一致


    [DllImport("dwrite.dll", PreserveSig = false)]
    private static extern void DWriteCreateFactory(DWRITE_FACTORY_TYPE factoryType, in Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);


    private enum DWRITE_FACTORY_TYPE
    {
        DWRITE_FACTORY_TYPE_SHARED = 0,
        DWRITE_FACTORY_TYPE_ISOLATED = 1,
    }


    [ComImport]
    [Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDWriteFactory
    {
        void GetSystemFontCollection(out IDWriteFontCollection fontCollection, [MarshalAs(UnmanagedType.Bool)] bool checkForUpdates);
    }


    [ComImport]
    [Guid("a84cee02-3eea-4eee-a827-87c1a02a0fcc")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDWriteFontCollection
    {
        [PreserveSig]
        uint GetFontFamilyCount();

        void GetFontFamily(uint index, out IDWriteFontFamily fontFamily);
    }


    [ComImport]
    [Guid("da20d8ef-812a-4c43-9802-62ec4abd7add")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDWriteFontFamily
    {
        void GetFontCollection([MarshalAs(UnmanagedType.IUnknown)] out object fontCollection);

        [PreserveSig]
        uint GetFontCount();

        void GetFont(uint index, [MarshalAs(UnmanagedType.IUnknown)] out object font);

        void GetFamilyNames(out IDWriteLocalizedStrings names);
    }


    [ComImport]
    [Guid("08256209-099a-4b34-b86d-c22b110e7771")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDWriteLocalizedStrings
    {
        [PreserveSig]
        uint GetCount();

        void FindLocaleName([MarshalAs(UnmanagedType.LPWStr)] string localeName, out uint index, [MarshalAs(UnmanagedType.Bool)] out bool exists);

        void GetLocaleNameLength(uint index, out uint nameLength);

        void GetLocaleName(uint index, IntPtr name, uint nameSize);

        void GetStringLength(uint index, out uint stringLength);

        void GetString(uint index, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder stringBuffer, uint stringSize);
    }


    #endregion

}
