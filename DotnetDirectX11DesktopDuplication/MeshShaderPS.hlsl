static const float ambientIntensity = 1;// 0.40f;
static const float DiffuseIntensity = 1;// 0.60f;
static const float SpecularIntensity = 0.65f;

struct PixelShaderInput
{
	float4 pos : SV_POSITION;
	float4 color: COLOR0;
	float3 view : TEXCOORD0;
	float3 norm : NORMAL0;
	float3 bary : NORMAL1;
};


float4 PS(PixelShaderInput input) : SV_TARGET
{
	float4 ambientColor = float4(0.1, 0.18725, 0.1745, 0.8); // input.color;
	float4 DiffuseColor = float4(0.396, 0.74151, 0.69102, 0.8); // input.color;
	float4 SpecColor = float4(0.297254,	0.30829,	0.306678,	0.8);

	float3 light = input.view;
	float3 normal = input.norm;

	float LN = saturate(dot(light, normal));
	float3 R = normalize(2 * LN * normal - light);

	float4 ambient = ambientColor * ambientIntensity;
	float4 diffuse = DiffuseColor * DiffuseIntensity * LN;

	float4 color = ambient + diffuse;

	//if (input.wire.y > 0)
	{
		float phong = SpecularIntensity * pow(saturate(dot(R, light)), 8);
		color += phong;
	}

	/*float d = 0.03f;
	if (input.bary.x < d
		|| input.bary.y < d
		|| input.bary.z < d)
	{
		color /= 1.5;
	}*/

	return saturate(color);
}