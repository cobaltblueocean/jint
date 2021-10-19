// Copyright (C) 2013 the V8 project authors. All rights reserved.
// This code is governed by the BSD license found in the LICENSE file.
/*---
es6id: 25.3.1.4
description: >
    When a generator is paused within a `try` block that is declared within a
    `try` block of a `try..catch` statement, `throw` should interrupt control
    flow as if a `throw` statement had appeared at that location in the
    function body.
features: [generators]
---*/

var unreachable = 0;
function* g() {
  try {
    yield 1;
    try {
      yield 2;
      unreachable += 1;
    } catch (e) {
      yield e;
    }
    yield 3;
  } finally {
    yield 4;
  }
  yield 5;
}
var iter = g();
var exception = new Error();
var result;

result = iter.next();
assert.sameValue(result.value, 1, 'First result value');
assert.sameValue(result.done, false, 'First result `done` flag');

result = iter.next();
assert.sameValue(result.value, 2, 'Second result `value`');
assert.sameValue(result.done, false, 'Second result `done` flag');

result = iter.throw(exception);
assert.sameValue(result.value, exception, 'Third result `value`');
assert.sameValue(result.done, false, 'Third result `done` flag');

assert.sameValue(
  unreachable,
  0,
  'statement following `yield` not executed (following `throw`)'
);

result = iter.next();
assert.sameValue(result.value, 3, 'Fourth result `value');
assert.sameValue(result.done, false, 'Fourth result `done` flag');

result = iter.next();
assert.sameValue(result.value, 4, 'Fifth result `value`');
assert.sameValue(result.done, false, 'Fifth result `done` flag');

result = iter.next();
assert.sameValue(result.value, 5, 'Sixth result `value`');
assert.sameValue(result.done, false, 'Sixth result `done` flag');

result = iter.next();
assert.sameValue(
  result.value, undefined, 'Result `value` is undefined when done'
);
assert.sameValue(result.done, true, 'Result `done` flag is `true` when done');
assert.sameValue(
  unreachable, 0, 'statement following `yield` not executed (once "completed")'
);

iter.next();
