using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using QuickPad.Files;

namespace QuickPad.UI;

// Edit mode: the editor, search, save and the inline dialogs.
public partial class GlassWindow
{
    private const double EditWidth = 600;
    private const double EditHeight = 440;
    private const string EditFooter = "Ctrl+S guarda · Ctrl+F busca · Esc cierra";

    private TextDocument? _doc;     // fully opened document being edited; null in preview
    private Task<bool>? _resolving; // the open "Tenés cambios sin guardar." question, shared by every caller
    private string _savedText = ""; // last loaded or saved text, line endings normalized

    public string? CurrentPath { get; private set; }

    /// <summary>True when the editor text differs from what was last loaded or saved.</summary>
    public bool IsDirty => _doc is not null && Normalize(Editor.Text) != _savedText;

    /// <summary>Opens <paramref name="path"/> centered on the monitor with the cursor, directly in edit mode.
    /// With unsaved changes it first asks, like <see cref="ShowPreview"/>.</summary>
    public async Task ShowEditor(string path)
    {
        if (!await ResolveUnsavedChangesAsync(bringForward: true)) return;
        LoadPreview(path);
        var workArea = WorkArea(System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
        Width = Math.Min(EditWidth, workArea.Width - 2 * Gap);
        Height = Math.Min(EditHeight, workArea.Height - 2 * Gap);
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
        Pop(new Point(0.5, 0.5));
        await EnterEditAsync(click: null, grow: false);
    }

    /// <summary>When there are unsaved changes, asks Guardar / Descartar / Seguir editando.
    /// True when the caller may go on (nothing to save, saved, or discarded). A request that arrives while
    /// the question is open waits for the same answer instead of replacing the dialog.</summary>
    /// <param name="bringForward">Activate the window first: the request came from outside (hook, "Abrir con…"),
    /// not from the user clicking away.</param>
    private Task<bool> ResolveUnsavedChangesAsync(bool bringForward = false)
    {
        if (!IsDirty) return Task.FromResult(true);
        if (bringForward && IsVisible) Activate();
        return _resolving ??= Resolve();

        async Task<bool> Resolve()
        {
            try
            {
                switch (await Ask("Tenés cambios sin guardar.", "Guardar", "Descartar", "Seguir editando"))
                {
                    case 0: return await SaveAsync();
                    case 1: _savedText = Normalize(Editor.Text); return true; // discarded: every caller now resets or hides
                    default: return false;
                }
            }
            finally { _resolving = null; }
        }
    }

    private async Task CloseAsync(bool bringForward = false)
    {
        if (await ResolveUnsavedChangesAsync(bringForward)) HideNow();
    }

    protected override async void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (Dialog.IsOpen)
        {
            if (e.Key == Key.Escape) { e.Handled = true; Dialog.Dismiss(); }
            return;
        }
        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (ctrl && e.Key == Key.S)
        {
            e.Handled = true;
            if (_doc is not null) await SaveAsync();
        }
        else if (ctrl && e.Key == Key.F)
        {
            e.Handled = true;
            await OpenSearchAsync();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (SearchBox.IsKeyboardFocused) CloseSearch();
            else await CloseAsync();
        }
    }

    private async void OnContentClick(object sender, MouseButtonEventArgs e)
    {
        if (_doc is not null || Dialog.IsOpen || PreviewText.Visibility != Visibility.Visible) return;
        await EnterEditAsync(e.GetPosition(PreviewText), grow: true);
    }

    /// <summary>Opens the whole file into the editor, asking first when it is large. <paramref name="click"/>
    /// (relative to the preview text, which the editor replaces in place) is where the caret goes.</summary>
    private async Task EnterEditAsync(Point? click, bool grow)
    {
        if (_doc is not null || CurrentPath is not { } path || PreviewText.Visibility != Visibility.Visible) return;
        if (!await OpenIntoEditorAsync(path)) return;
        PreviewText.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Visible;
        Footer.Text = EditFooter;
        if (grow) Grow();
        Editor.Focus();
        Editor.UpdateLayout();
        int caret = click is { } p ? Editor.GetCharacterIndexFromPoint(p, snapToText: true) : 0;
        Editor.CaretIndex = Math.Max(caret, 0);
    }

    /// <summary>Reads the whole file into the editor, asking first when it is large. False (after telling
    /// the user why, when it failed) when nothing was loaded; the editor then keeps its text.</summary>
    private async Task<bool> OpenIntoEditorAsync(string path)
    {
        string error;
        try
        {
            long length = new FileInfo(path).Length; // before Open: a full Open allocates the whole file
            if (length > TextFile.EditSizeLimit
                && await Ask($"Es un archivo grande ({length / (1024.0 * 1024):0.#} MB). ¿Editarlo igual?", "Editar", "Cancelar") != 0)
                return false;
            if (CurrentPath != path) return false; // another file was shown while asking
            LoadEditor(TextFile.Open(path));
            return true;
        }
        catch (NotTextException) { error = "Este archivo no es texto"; }
        catch (OutOfMemoryException) { error = "El archivo es demasiado grande para editarlo."; } // > 2 GB, or no room
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = "No se pudo abrir el archivo"; }
        await Ask(error, "Aceptar");
        return false;
    }

    private void LoadEditor(TextDocument doc)
    {
        int caret = Editor.CaretIndex;
        Editor.Text = doc.Text; // first: if this throws, the editor and _doc still match
        _doc = doc;
        _savedText = Normalize(doc.Text);
        Editor.IsUndoEnabled = false; // clears the undo stack: undo must not go back past the loaded text
        Editor.IsUndoEnabled = true;
        Editor.CaretIndex = Math.Min(caret, Editor.Text.Length);
    }

    /// <summary>Back to preview state; frees the edited text.</summary>
    private void ResetEditor()
    {
        Dialog.Dismiss();
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        _doc = null;
        _savedText = "";
        Editor.IsReadOnly = false;
        Editor.Clear();
        Editor.Visibility = Visibility.Collapsed;
        CloseSearch();
    }

    /// <summary>Grows the window to at least the edit size in 200 ms (a long preview is already taller),
    /// kept inside the work area of its monitor.</summary>
    private void Grow()
    {
        var workArea = WorkArea(System.Windows.Forms.Screen.FromHandle(_hwnd));
        double width = Math.Min(Math.Max(ActualWidth, EditWidth), workArea.Width - 2 * Gap);
        double height = Math.Min(Math.Max(ActualHeight, EditHeight), workArea.Height - 2 * Gap);
        Left = Math.Clamp(Left, workArea.Left, workArea.Right - width);
        Top = Math.Clamp(Top, workArea.Top, workArea.Bottom - height);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        foreach (var (property, to) in new[] { (WidthProperty, width), (HeightProperty, height) })
        {
            double from = (double)GetValue(property);
            SetValue(property, to); // base value, so it stays once the animation (FillBehavior.Stop) ends
            BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }
    }

    /// <summary>Saves the editor text. True when it was written. On failure the text stays in the editor.</summary>
    private async Task<bool> SaveAsync(bool force = false)
    {
        if (_doc is null) return false;
        try
        {
            _doc = TextFile.Save(_doc, Editor.Text, force);
            _savedText = Normalize(_doc.Text);
            SavedMark.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(800))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
            return true;
        }
        catch (FileChangedExternallyException)
        {
            switch (await Ask("El archivo cambió afuera.", "Sobrescribir", "Recargar"))
            {
                case 0: return await SaveAsync(force: true);
                case 1: await ReloadAsync(); return false;
                default: return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            await Ask("No tenés permiso para guardar este archivo (solo lectura o protegido).", "Aceptar");
        }
        catch (EncoderFallbackException)
        {
            await Ask("Este archivo usa una codificación antigua que no admite algunos caracteres que escribiste.", "Aceptar");
        }
        catch (IOException ex)
        {
            await Ask(ex.Message, "Aceptar");
        }
        return false;
    }

    /// <summary>Replaces the editor text with the file on disk (the user chose to drop their changes).</summary>
    private async Task ReloadAsync()
    {
        if (_doc is not null) await OpenIntoEditorAsync(_doc.Path);
    }

    /// <summary>Shows an inline dialog, then gives the keyboard back to the editor.</summary>
    private async Task<int> Ask(string message, params string[] buttons)
    {
        var answer = Dialog.Ask(message, buttons);
        Footer.Visibility = Visibility.Hidden; // it would show through the strip
        int choice = await answer;
        if (Dialog.IsOpen) return choice; // a newer dialog replaced this one
        Footer.Visibility = Visibility.Visible;
        if (_doc is not null) Editor.Focus();
        return choice;
    }

    private async Task OpenSearchAsync()
    {
        await EnterEditAsync(click: null, grow: true);
        if (_doc is null) return;
        SearchBar.Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void CloseSearch()
    {
        bool hadFocus = SearchBox.IsKeyboardFocused;
        SearchBar.Visibility = Visibility.Collapsed;
        MatchMark.Visibility = Visibility.Collapsed;
        if (hadFocus) Editor.Focus();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        bool backwards = Keyboard.Modifiers == ModifierKeys.Shift;
        int from = backwards ? Editor.SelectionStart : Editor.SelectionStart + Editor.SelectionLength;
        int i = TextSearch.FindNext(Editor.Text, SearchBox.Text, from, backwards);
        SearchStatus.Text = i < 0 && SearchBox.Text.Length > 0 ? "Sin resultados" : "";
        if (i < 0) return;
        Editor.Select(i, SearchBox.Text.Length);
        Editor.ScrollToLine(Editor.GetLineIndexFromCharacterIndex(i));
        Editor.UpdateLayout();
        UpdateMatchMark();
    }

    private void OnEditorViewChanged(object sender, RoutedEventArgs e) => UpdateMatchMark();

    /// <summary>Draws the accent mark over the selection while the search box, not the editor, has the focus.</summary>
    private void UpdateMatchMark()
    {
        var start = Editor.GetRectFromCharacterIndex(Editor.SelectionStart);
        if (SearchBar.Visibility != Visibility.Visible || Editor.IsKeyboardFocused || Editor.SelectionLength == 0 || start.IsEmpty)
        {
            MatchMark.Visibility = Visibility.Collapsed;
            return;
        }
        var end = Editor.GetRectFromCharacterIndex(Editor.SelectionStart + Editor.SelectionLength - 1, trailingEdge: true);
        var topLeft = Editor.TranslatePoint(start.TopLeft, MatchLayer);
        Canvas.SetLeft(MatchMark, topLeft.X);
        Canvas.SetTop(MatchMark, topLeft.Y);
        MatchMark.Width = Math.Max(0, end.X - start.X);
        MatchMark.Height = start.Height;
        MatchMark.Visibility = Visibility.Visible;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchStatus.Text = "";
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
