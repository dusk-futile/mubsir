using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Basira.Core.Speech;

namespace Basira.App.Speech;

/// <summary>
/// NVDA's controller client (nvdaControllerClient.dll, LGPL-2.1, shipped next to Basira.exe).
/// Signatures follow nvdaController.h from the NVDA 2026.2 controllerClient package. Every function returns a
/// Windows error code (0 = success). speakSsml and getProcessId need NVDA 2024.1+; older NVDA returns 1717.
/// </summary>
internal static unsafe partial class NvdaNative
{
    private const string Dll = "nvdaControllerClient";

    [LibraryImport(Dll)]
    public static partial uint nvdaController_testIfRunning();

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint nvdaController_speakText(string text);

    [LibraryImport(Dll)]
    public static partial uint nvdaController_cancelSpeech();

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint nvdaController_brailleMessage(string message);

    [LibraryImport(Dll)]
    public static partial uint nvdaController_getProcessId(out uint pid);

    /// <param name="ssml">SSML document.</param>
    /// <param name="symbolLevel">SYMBOL_LEVEL; -1 keeps NVDA's setting.</param>
    /// <param name="priority">SPEECH_PRIORITY; 0 = normal.</param>
    /// <param name="asynchronous">MIDL <c>boolean</c> (one byte). False waits until NVDA has spoken it.</param>
    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint nvdaController_speakSsml(string ssml, int symbolLevel, int priority, [MarshalAs(UnmanagedType.U1)] bool asynchronous);

    [LibraryImport(Dll)]
    public static partial uint nvdaController_setOnSsmlMarkReachedCallback(delegate* unmanaged[Stdcall]<char*, uint> callback);
}

/// <summary>
/// Safe wrapper over <see cref="NvdaNative"/>: a missing or broken DLL turns the NVDA path off instead of crashing.
/// SSML marks are routed to the current <see cref="SpeechHistory"/>.
/// </summary>
internal sealed unsafe class NvdaController
{
    /// <summary>RPC_S_UNKNOWN_IF: NVDA older than 2024.1 does not know speakSsml/getProcessId.</summary>
    public const uint ErrorUnknownInterface = 1717;

    /// <summary>Returned by this wrapper when the DLL or an export is missing.</summary>
    public const uint ErrorDllUnavailable = 126; // ERROR_MOD_NOT_FOUND

    private static SpeechHistory? s_markTarget;

    private volatile bool _dllMissing;
    private bool _callbackSet;

    /// <summary>False once the DLL or one of its exports could not be loaded.</summary>
    public bool DllAvailable => !_dllMissing;

    /// <summary>Where mark callbacks go (the speech service's history).</summary>
    public static void SetMarkTarget(SpeechHistory? history) => Volatile.Write(ref s_markTarget, history);

    /// <summary>0 when NVDA is running.</summary>
    public uint TestIfRunning() => Call(static () => NvdaNative.nvdaController_testIfRunning());

    public bool IsRunning() => TestIfRunning() == 0;

    public uint SpeakText(string text) => Call(() => NvdaNative.nvdaController_speakText(text));

    public uint CancelSpeech() => Call(static () => NvdaNative.nvdaController_cancelSpeech());

    public uint BrailleMessage(string message) => Call(() => NvdaNative.nvdaController_brailleMessage(message));

    /// <summary>Speaks SSML; with <paramref name="asynchronous"/> false the call returns after NVDA spoke (or was cancelled).</summary>
    public uint SpeakSsml(string ssml, int symbolLevel = -1, int priority = 0, bool asynchronous = false)
    {
        EnsureMarkCallback();
        return Call(() => NvdaNative.nvdaController_speakSsml(ssml, symbolLevel, priority, asynchronous));
    }

    /// <summary>NVDA's process id, or 0 when unavailable.</summary>
    public uint GetProcessId()
    {
        uint pid = 0;
        uint rc = Call(() => NvdaNative.nvdaController_getProcessId(out pid));
        return rc == 0 ? pid : 0;
    }

    private void EnsureMarkCallback()
    {
        if (_callbackSet || _dllMissing)
        {
            return;
        }

        // The callback pointer lives in the client DLL; it is invoked during a synchronous speakSsml call.
        uint rc = Call(static () => NvdaNative.nvdaController_setOnSsmlMarkReachedCallback(&OnMarkReached));
        _callbackSet = rc == 0;
    }

    private uint Call(Func<uint> call)
    {
        if (_dllMissing)
        {
            return ErrorDllUnavailable;
        }

        try
        {
            return call();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _dllMissing = true;
            return ErrorDllUnavailable;
        }
        catch (SEHException)
        {
            return ErrorDllUnavailable;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint OnMarkReached(char* mark)
    {
        try
        {
            if (mark is not null
                && Volatile.Read(ref s_markTarget) is SpeechHistory history
                && SpeechComposer.TryParseMark(new string(mark), out int index))
            {
                history.MarkSpoken(index);
            }
        }
        catch
        {
            // Nothing may escape into native code.
        }

        return 0;
    }
}
