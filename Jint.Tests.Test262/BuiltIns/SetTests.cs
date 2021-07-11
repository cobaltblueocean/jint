﻿using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class SetTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Set")]
        [MemberData(nameof(SourceFiles), "built-ins\\Set", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Set", true, Skip = "Skipped")]
        protected void Set(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\SetIteratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\SetIteratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\SetIteratorPrototype", true, Skip = "Skipped")]
        protected void SetIteratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}