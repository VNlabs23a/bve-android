# ProGuard rules for OpenBVE-Android

# Keep Godot classes
-keep class org.godotengine.godot.** { *; }
-keep class com.godot.game.** { *; }

# Keep Godot native interfaces
-keep class org.godotengine.godot.GodotLib { *; }
-keep class org.godotengine.godot.GodotRuntime { *; }

# Keep serialization
-keepclassmembers class * implements java.io.Serializable {
    static final long serialVersionUID;
    private static final java.io.ObjectStreamField[] serialPersistentFields;
    !static !transient <fields>;
    private void writeObject(java.io.ObjectOutputStream);
    private void readObject(java.io.ObjectInputStream);
    java.lang.Object writeReplace();
    java.lang.Object readResolve();
}

# Keep enums
-keepclassmembers enum * {
    public static **[] values();
    public static ** valueOf(java.lang.String);
}

# Keep native methods
-keepclasseswithmembernames class * {
    native <methods>;
}

# Keep JNI classes
-keep class * {
    native <methods>;
}

# Keep Godot's JNI bridge
-keep class org.godotengine.godot.GodotLib {
    *;
}

# Keep Android framework classes used by Godot
-keep class android.app.Activity { *; }
-keep class android.content.Context { *; }
-keep class android.content.res.AssetManager { *; }
-keep class android.graphics.* { *; }
-keep class android.media.* { *; }
-keep class android.opengl.* { *; }
-keep class android.os.* { *; }
-keep class android.view.* { *; }

# Keep Kotlin metadata
-keep class kotlin.Metadata { *; }
-keep class kotlin.coroutines.** { *; }

# Keep Coroutines
-keepclassmembers class kotlinx.coroutines.** { *; }

# Keep annotation processors
-keep @interface * { *; }

# Keep SharpCompress (used for mod archives)
-keep class org.sharpcompress.** { *; }
-dontwarn org.sharpcompress.**

# Keep NVorbis (audio)
-keep class NVorbis.** { *; }
-dontwarn NVorbis.**

# Keep NAudio (audio)
-keep class NAudio.** { *; }
-dontwarn NAudio.**

# Keep System.Text.Json
-keep class System.Text.Json.** { *; }

# Keep OpenBVE-Android classes
-keep class OpenBVEAndroid.** { *; }

# Keep reflection used by Godot
-keepclassmembers class * {
    @godot.* *;
}

# Optimize
-optimizationpasses 5
-allowaccessmodification
-mergelibraryclasses
-overloadaggressively

# Don't warn about missing classes
-dontwarn javax.annotation.**
-dontwarn kotlin.reflect.**
-dontwarn org.codehaus.mojo.animal_sniffer.IgnoreJRERequirement
-dontwarn sun.misc.Unsafe

# Keep line numbers for debugging (release builds)
-keepattributes SourceFile,LineNumberTable

# Keep annotations
-keepattributes *Annotation*,Signature,InnerClasses,EnclosingMethod