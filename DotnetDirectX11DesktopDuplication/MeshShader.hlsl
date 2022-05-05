// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved
//----------------------------------------------------------------------
cbuffer ModelViewProjectionConstantBuffer : register(b0)
{
    matrix model;
    matrix view;
    matrix projection;

    matrix WorldInverseTranspose;
    float4 vecEye;
};

struct VS_INPUT
{
    float3 Pos : POSITION;
    float4 Color : COLOR;
    float3 Normal : NORMAL0;
};

struct VS_OUTPUT
{
    float4 Pos : SV_POSITION;
    float4 Color : COLOR;

    float3 view : TEXCOORD0;
    float3 norm : NORMAL0;
    float3 bary : NORMAL1;
};

//--------------------------------------------------------------------------------------
// Vertex Shader
//--------------------------------------------------------------------------------------
VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 pos = float4(input.Pos,1.0f);
    pos = mul(pos, model);
    pos = mul(pos, view);
    pos = mul(pos, projection);

    output.Pos = pos;
    output.Color = input.Color;

    output.norm = normalize(mul(WorldInverseTranspose, input.Normal));
    output.view = normalize(pos - vecEye);
    output.bary = float3(input.Color.x, input.Color.y, input.Color.z);

    return output;
}