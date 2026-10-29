import QRCode from 'qrcode';

/** Banks offered in the property editor. */
export const BANK_NAMES = ['Vietcombank', 'Techcombank', 'MB', 'BIDV', 'Agribank', 'VPBank', 'ACB', 'TPBank', 'Sacombank', 'VietinBank', 'VIB', 'HDBank', 'OCB', 'MSB', 'SHB', 'SCB'];

/** Bank BIN codes (NAPAS) for common Vietnamese banks, keyed by normalized name fragment. */
const BANK_BINS: [string, string][] = [
  ['vietcombank', '970436'], ['vcb', '970436'],
  ['techcombank', '970407'], ['tcb', '970407'],
  ['mbbank', '970422'], ['mb bank', '970422'], ['mb', '970422'],
  ['bidv', '970418'],
  ['agribank', '970405'],
  ['vpbank', '970432'],
  ['acb', '970416'],
  ['tpbank', '970423'],
  ['sacombank', '970403'],
  ['vietinbank', '970415'],
  ['vib', '970441'],
  ['hdbank', '970437'],
  ['ocb', '970448'],
  ['msb', '970426'],
  ['shb', '970443'],
  ['scb', '970429'],
];

function normalize(text: string): string {
  return text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
}

/** Returns the 6-digit BIN for a bank name or code, or null when it cannot be resolved. */
export function bankBin(bankName: string | null | undefined): string | null {
  if (!bankName) return null;
  const trimmed = bankName.trim();
  if (/^\d{6}$/.test(trimmed)) return trimmed;
  const n = normalize(trimmed);
  const tokens = n.split(/[^a-z0-9]+/);
  const hit = BANK_BINS.find(([key]) => tokens.includes(key) || (key.length >= 4 && n.includes(key)));
  return hit ? hit[1] : null;
}

/** Transfer content: removes diacritics and characters that banks reject, limited to 50 chars. */
export function transferContent(text: string): string {
  return normalize(text).replace(/[^a-z0-9 -]/g, '').replace(/\s+/g, ' ').trim().toUpperCase().slice(0, 50);
}

function tlv(id: string, value: string): string {
  return id + value.length.toString().padStart(2, '0') + value;
}

/** CRC-16/CCITT-FALSE as required by EMVCo QR payloads. */
function crc16(input: string): string {
  let crc = 0xffff;
  for (let i = 0; i < input.length; i++) {
    crc ^= input.charCodeAt(i) << 8;
    for (let b = 0; b < 8; b++) {
      crc = crc & 0x8000 ? ((crc << 1) ^ 0x1021) & 0xffff : (crc << 1) & 0xffff;
    }
  }
  return crc.toString(16).toUpperCase().padStart(4, '0');
}

/** Builds a VietQR (EMVCo) bank transfer payload with a fixed amount and transfer content. */
export function buildVietQrPayload(opts: { bin: string; accountNumber: string; amount: number; content: string }): string {
  const merchant = tlv('00', 'A000000727') + tlv('01', tlv('00', opts.bin) + tlv('01', opts.accountNumber)) + tlv('02', 'QRIBFTTA');
  const body =
    tlv('00', '01') +
    tlv('01', '12') +
    tlv('38', merchant) +
    tlv('53', '704') +
    tlv('54', Math.round(opts.amount).toString()) +
    tlv('58', 'VN') +
    tlv('62', tlv('08', opts.content)) +
    '6304';
  return body + crc16(body);
}

/** Renders a VietQR payload as a PNG data URL. */
export function renderQr(payload: string): Promise<string> {
  return QRCode.toDataURL(payload, { margin: 1, width: 256 });
}
