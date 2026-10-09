export class SupportDraft {
  constructor() { this.reset(); }
  reset() { this.requestId = crypto.randomUUID(); this.fingerprint = ''; this.inFlight = false; }
  begin(fields) {
    if (this.inFlight) return null;
    const normalized = Object.fromEntries(['topic','contact','message'].map(key => [key, String(fields[key] || '').trim().normalize('NFC')]));
    const fingerprint = JSON.stringify(normalized);
    if (this.fingerprint && this.fingerprint !== fingerprint) this.requestId = crypto.randomUUID();
    this.fingerprint = fingerprint;
    this.inFlight = true;
    return {requestId:this.requestId, ...normalized};
  }
  release() { this.inFlight = false; }
}

export function supportError(status, code) {
  if (status === 429) return 'Слишком много обращений. Пожалуйста, попробуйте позже.';
  if (code === 'invalid_support_contact') return 'Укажите почту или имя в Telegram, начиная с @.';
  if (code === 'invalid_support_message' || status === 413) return 'Опишите вопрос: от 20 до 4000 символов.';
  if (status === 403) return 'Повторите проверку перед отправкой.';
  if (status === 409) return 'Текст изменился. Повторите отправку.';
  if (status === 503) return 'Сейчас не получилось отправить вопрос. Попробуйте позже или откройте контакты.';
  return 'Не получилось отправить вопрос. Текст сохранён — попробуйте ещё раз.';
}
