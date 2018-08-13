// Copyright (C) 2015 André Bargull. All rights reserved.
// This code is governed by the BSD license found in the LICENSE file.

/*---
esid: sec-dataview.prototype.setfloat64
es6id: 24.2.4.14
description: >
  DataView.prototype.setFloat64.name is "setFloat64".
info: |
  DataView.prototype.setFloat64 ( byteOffset, value [ , littleEndian ] )

  17 ECMAScript Standard Built-in Objects:
    Every built-in Function object, including constructors, that is not
    identified as an anonymous function has a name property whose value
    is a String.

    Unless otherwise specified, the name property of a built-in Function
    object, if it exists, has the attributes { [[Writable]]: false,
    [[Enumerable]]: false, [[Configurable]]: true }.
includes: [propertyHelper.js]
---*/

assert.sameValue(DataView.prototype.setFloat64.name, "setFloat64");

verifyNotEnumerable(DataView.prototype.setFloat64, "name");
verifyNotWritable(DataView.prototype.setFloat64, "name");
verifyConfigurable(DataView.prototype.setFloat64, "name");
