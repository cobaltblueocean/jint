﻿using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed partial class EvalFunctionInstance : FunctionInstance
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}