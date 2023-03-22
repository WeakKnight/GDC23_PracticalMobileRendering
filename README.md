# Practical High-Performance Rendering on Mobile Platforms (GDC 2023)

![Teaser](https://user-images.githubusercontent.com/12985760/226530860-dfdbb68e-ed00-40d3-8c3b-035e9b24179e.png)

## Introduction

This repo includes the demo project for the following GDC 2023 talk 
> Practical High-Performance Rendering on Mobile Platforms<br>
> https://schedule.gdconf.com/session/practical-high-performance-rendering-on-mobile-platforms/890038

This project contains three parts: a **Scriptable Render Pipeline** named **PMRP**, a **Visibility Baking Tool**, and a **Lightmapper**. 

## Team Members
* [guoxx](https://github.com/guoxx) -
  **Xiaoxin Guo** <<guoxx@me.com>> (he/him)
* [weakknight](https://github.com/weakknight) - **Tianyu Li** <<ltyucb@gmail.com>> (he/him)

## Frame Dissecting
![LightingComponents](https://user-images.githubusercontent.com/12985760/226530905-2fe9cad7-292f-4e45-a42a-547c9fdaa8ae.gif)

## Render Pipeline
#### Feature
- Highly optimized for **Mobile Platforms**
- **Specular Occlusion**
- Platform-agnostic **Shadow Bias**

## Visibility Tool (Will Be Available Soon)
#### Feature
- **Visibility Baking** with least square vertex optimization

## Lightmapper
#### Feature
- **[Dynamic Baking](https://cs.dartmouth.edu/wjarosz/publications/seyb20uberbake.html)**

https://user-images.githubusercontent.com/12985760/226519094-62041dce-ac77-4e8b-b786-c23ef7421bd9.mp4

- **Lightmap** and **Volumetric Lightmap**
- **Path Tracing** integrator with **Resampled Importance Sampling** + **Light BVH** + **Irradiance Caching**
- Including **Specular-To-Diffuse** light paths

#### How-to-use
![](https://user-images.githubusercontent.com/12985760/226530960-0d412391-b300-4962-b008-8d721c85096b.png)

## Prerequisites
- [Unity 2023.1.0b6](https://unity.com/releases/editor/beta/2023.1.0b6) or newer
- Windows 10 SDK version 10.0.19041.1 or newer
- Graphics card with raytracing support
