#if ANDROID
using System;

namespace MelonLoader.Java;

public class JObject : IDisposable
{
    private bool Disposed { get; set; }

    public IntPtr Handle { get; set;}

    internal JNI.ReferenceType ReferenceType { get; set;}

    public JObject() { }

    public JObject(IntPtr handle, JNI.ReferenceType referenceType)
    {
        this.Handle = handle;
        this.ReferenceType = referenceType;
    }

    public JObject(JObject obj) : this(obj.Handle, obj.ReferenceType) 
    {
        this.Disposed = obj.Disposed;
        obj.Disposed = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed || this.Handle == IntPtr.Zero)
            return;

        switch (this.ReferenceType)
        {
            case JNI.ReferenceType.Local:
                JNI.DeleteLocalRef(this);
                break;

            case JNI.ReferenceType.Global:
                JNI.DeleteGlobalRef(this);
                break;

            case JNI.ReferenceType.WeakGlobal:
                JNI.DeleteWeakGlobalRef(this);
                break;
        }

        Disposed = true;
    }

    public bool Valid()
    {
        return this.Handle != IntPtr.Zero;
    }

    // No finalizer: references may only be released from a thread attached to the JVM, and the .NET finalizer thread
    // isn't one. Undisposed references stay alive until the process exits, which is harmless for the few we create.

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
#endif
