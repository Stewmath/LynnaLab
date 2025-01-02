using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Veldrid.Sdl2;

namespace VeldridBackend;

// Helper class for setting the SDL window icon.
// Taken from: https://github.com/veldrid/veldrid/issues/138
class ApplicationIcon
{
	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private unsafe delegate IntPtr SDL_RWFromFile_t( byte* file, byte* mode );
	private static SDL_RWFromFile_t s_RWFromFile = Sdl2Native.LoadFunction< SDL_RWFromFile_t >( "SDL_RWFromFile" );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate IntPtr SDL_LoadBMP_RW_t( IntPtr src, int freesrc );
	private static SDL_LoadBMP_RW_t s_LoadBMP_RW = Sdl2Native.LoadFunction< SDL_LoadBMP_RW_t >( "SDL_LoadBMP_RW" );
	private static IntPtr SDL_LoadBMP_RW( IntPtr src, int freesrc ) => s_LoadBMP_RW( src, freesrc );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate IntPtr SDL_SetWindowIcon_t( IntPtr window, IntPtr surface );
	private static SDL_SetWindowIcon_t s_SetWindowIcon = Sdl2Native.LoadFunction< SDL_SetWindowIcon_t >( "SDL_SetWindowIcon" );
	private static void SDL_SetWindowIcon( IntPtr window, IntPtr surface ) => s_SetWindowIcon( window, surface );

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate IntPtr SDL_FreeSurface_t( IntPtr surface );
	private static SDL_FreeSurface_t s_FreeSurface = Sdl2Native.LoadFunction< SDL_FreeSurface_t >( "SDL_FreeSurface" );
	private static void SDL_FreeSurface( IntPtr surface ) => s_FreeSurface( surface );

	private static unsafe IntPtr SDL_RWFromFile(
		string file,
		string mode )
	{
		int fileByteCount = Encoding.UTF8.GetByteCount( file );
		byte* utf8FileBytes = stackalloc byte[ fileByteCount + 1 ];
		fixed ( char* filePtr = file )
		{
			int actualBytes = Encoding.UTF8.GetBytes(
				filePtr,
				file.Length,
				utf8FileBytes,
				fileByteCount );
			utf8FileBytes[ actualBytes ] = 0;
		}

		int modeByteCount = Encoding.UTF8.GetByteCount( mode );
		byte* utf8ModeBytes = stackalloc byte[ modeByteCount + 1 ];
		fixed ( char* modePtr = mode )
		{
			int actualBytes = Encoding.UTF8.GetBytes(
				modePtr,
				mode.Length,
				utf8ModeBytes,
				modeByteCount );
			utf8ModeBytes[ actualBytes ] = 0;
		}

		return s_RWFromFile(
			utf8FileBytes,
			utf8ModeBytes );
	}

	private static IntPtr SDL_LoadBMP(
		string file )
	{
		IntPtr rwOps = SDL_RWFromFile(
			file,
			"rb" );
		return SDL_LoadBMP_RW(
			rwOps,
			1 );
	}

	internal static unsafe void SetWindowIcon(
		IntPtr sdlWindow,
		string path )
	{
		if ( !File.Exists( path ) )
		{
			throw new Exception( $"Application icon not found at '{path}'" );
		}

		IntPtr icon = SDL_LoadBMP( path );
		if ( IntPtr.Zero != icon )
		{
			SDL_SetWindowIcon(
				sdlWindow,
				icon );
			SDL_FreeSurface( icon );
			return;
		}

		byte* error = Sdl2Native.SDL_GetError( );
		if ( error == null )
		{
			return;
		}

		int chars = 0;
		while ( error[ chars ] != 0 )
		{
			chars++;
		}

		throw new Exception( $"Failed to load application icon: {Encoding.UTF8.GetString( error, chars )}" );
	}
}