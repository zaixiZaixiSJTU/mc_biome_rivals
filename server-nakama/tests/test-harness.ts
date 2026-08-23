namespace TestHarness {
  let passed = 0;
  let failed = 0;

  export function equal<T>(actual: T, expected: T, message?: string): void {
    if (actual !== expected) {
      throw new Error((message ? message + ': ' : '') + 'expected ' + String(expected) + ', received ' + String(actual));
    }
  }

  export function ok(value: unknown, message?: string): void {
    if (!value) throw new Error(message || 'expected a truthy value');
  }

  export function test(name: string, body: () => void): void {
    try {
      body();
      passed += 1;
      console.log('PASS ' + name);
    } catch (error) {
      failed += 1;
      console.error('FAIL ' + name + '\n  ' + String(error));
    }
  }

  export function finish(): void {
    console.log('\n' + passed + ' passed, ' + failed + ' failed');
    if (failed > 0) throw new Error('test suite failed');
  }
}
