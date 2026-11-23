#include <emscripten/html5_webgl.h>

EMSCRIPTEN_RESULT sundex_create_webgl2_context(void)
{
    EmscriptenWebGLContextAttributes attributes;
    emscripten_webgl_init_context_attributes(&attributes);
    attributes.majorVersion = 2;
    attributes.antialias = EM_FALSE;

    EMSCRIPTEN_WEBGL_CONTEXT_HANDLE context = emscripten_webgl_create_context("#sundex", &attributes);
    if (!context)
        return EMSCRIPTEN_RESULT_NOT_SUPPORTED;

    EMSCRIPTEN_RESULT result = emscripten_webgl_make_context_current(context);
    if (result != EMSCRIPTEN_RESULT_SUCCESS)
        emscripten_webgl_destroy_context(context);

    return result;
}
