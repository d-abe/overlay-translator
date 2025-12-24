using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;

namespace OverlayTranslator.Controls
{
    /// <summary>
    /// 十字カーソルを表示するCanvas
    /// ProtectedCursorプロパティを使用してカーソルを設定
    /// </summary>
    public class CrosshairCanvas : Canvas
    {
        public CrosshairCanvas()
        {
            // 十字カーソルを設定
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
        }
    }
}

