float decode(float v) { return v <= .04045 ? v / 12.92 : pow(max((v + .055) / 1.055, 0), 2.4); }
float encode(float v) { return v <= .0031308 ? v * 12.92 : 1.055 * pow(max(v, 0), 1.0 / 2.4) - .055; }
float3 decode3(float3 v) { return float3(decode(v.r), decode(v.g), decode(v.b)); }
float3 encode3(float3 v) { return float3(encode(v.r), encode(v.g), encode(v.b)); }
