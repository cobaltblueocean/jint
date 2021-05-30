﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.CallStack;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    internal static class ExceptionHelper
    {
        public static T ThrowSyntaxError<T>(Realm realm, string message = null)
        {
            ThrowSyntaxError(realm, message);
            return default;
        }

        [DoesNotReturn]
        public static void ThrowSyntaxError(Realm realm, string message = null)
        {
            throw new JavaScriptException(realm.Intrinsics.SyntaxError, message);
        }

        public static T ThrowArgumentException<T>(string message = null)
        {
            ThrowArgumentException(message);
            return default;
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(string message = null)
        {
            ThrowArgumentException(message, null);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(string message, string paramName)
        {
            throw new ArgumentException(message, paramName);
        }

        [DoesNotReturn]
        public static void ThrowReferenceError(Realm realm, Reference reference)
        {
            ThrowReferenceError(realm, reference?.GetReferencedName()?.ToString());
        }

        [DoesNotReturn]
        public static void ThrowReferenceError(Realm realm, string name)
        {
            var message = name != null ? name + " is not defined" : null;
            throw new JavaScriptException(realm.Intrinsics.ReferenceError, message);
        }

        public static T ThrowTypeErrorNoEngine<T>(string message = null, Exception exception = null)
        {
            throw new TypeErrorException(message);
        }

        public static T ThrowReferenceError<T>(Realm realm, string message = null)
        {
            throw new JavaScriptException(realm.Intrinsics.ReferenceError, message);
        }

        public static T ThrowTypeError<T>(Realm realm, string message = null, Exception exception = null)
        {
            ThrowTypeError(realm, message, exception);
            return default;
        }

        [DoesNotReturn]
        public static void ThrowTypeError(Realm realm, string message = null, Exception exception = null)
        {
            throw new JavaScriptException(realm.Intrinsics.TypeError, message, exception);
        }

        public static T ThrowRangeError<T>(Realm realm, string message = null)
        {
            throw new JavaScriptException(realm.Intrinsics.RangeError, message);
        }

        [DoesNotReturn]
        public static void ThrowRangeError(Realm realm, string message = null)
        {
            throw new JavaScriptException(realm.Intrinsics.RangeError, message);
        }

        [DoesNotReturn]
        public static void ThrowUriError(Realm realm)
        {
            throw new JavaScriptException(realm.Intrinsics.UriError);
        }

        [DoesNotReturn]
        public static void ThrowNotImplementedException(string message = null)
        {
            throw new NotImplementedException(message);
        }

        public static T ThrowNotImplementedException<T>()
        {
            throw new NotImplementedException();
        }

        public static T ThrowArgumentOutOfRangeException<T>()
        {
            throw new ArgumentOutOfRangeException();
        }

        public static T ThrowArgumentOutOfRangeException<T>(string paramName, string message)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException(string paramName, string message)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        [DoesNotReturn]
        public static void ThrowTimeoutException()
        {
            throw new TimeoutException();
        }

        [DoesNotReturn]
        public static void ThrowStatementsCountOverflowException()
        {
            throw new StatementsCountOverflowException();
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }

        public static T ThrowNotSupportedException<T>(string message = null)
        {
            throw new NotSupportedException(message);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException(string message = null)
        {
            throw new NotSupportedException(message);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException(string message = null)
        {
            throw new InvalidOperationException(message);
        }
        
        public static T ThrowInvalidOperationException<T>(string message = null)
        {
            throw new InvalidOperationException(message);
        }
        
        public static T ThrowPromiseRejectedException<T>(JsValue error)
        {
            throw new PromiseRejectedException(error);
        }

        [DoesNotReturn]
        public static void ThrowJavaScriptException(Engine engine, JsValue value, in Completion result)
        {
            throw new JavaScriptException(value).SetCallstack(engine, result.Location);
        }

        [DoesNotReturn]
        public static void ThrowRecursionDepthOverflowException(JintCallStack currentStack, string currentExpressionReference)
        {
            throw new RecursionDepthOverflowException(currentStack, currentExpressionReference);
        }

        [DoesNotReturn]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        public static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [DoesNotReturn]
        public static void ThrowMeaningfulException(Engine engine, TargetInvocationException exception)
        {
            var meaningfulException = exception.InnerException ?? exception;

            var handler = engine.Options._ClrExceptionsHandler;
            if (handler != null && handler(meaningfulException))
                ThrowError(engine, meaningfulException.Message);

            throw meaningfulException;
        }

        [DoesNotReturn]
        public static void ThrowError(Engine engine, string message)
        {
            throw new JavaScriptException(engine.Realm.Intrinsics.Error, message);
        }

        [DoesNotReturn]
        public static void ThrowPlatformNotSupportedException(string message)
        {
            throw new PlatformNotSupportedException(message);
        }

        [DoesNotReturn]
        public static void ThrowMemoryLimitExceededException(string message)
        {
            throw new MemoryLimitExceededException(message);
        }

        [DoesNotReturn]
        public static void ThrowExecutionCanceledException()
        {
            throw new ExecutionCanceledException();
        }
    }
}