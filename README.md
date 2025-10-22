# <span style="font-size: 24px;">Virtual Human as Living Partner: Mobile Augmented Reality Companion</span>

This repository contains the code and assets for the **Virtual Human as Living Partner** project, a mobile augmented reality (AR) system that brings a virtual human into the user's physical environment. Through timeline-based state scheduling and by aligning the camera to pre-calibrated markers, the system dynamically instantiates, animates, and locates the virtual human in real space. The goal is to transform this agent into a living companion that provides continuous presence, context-aware behavior, and interactive companionship. For a specific demo, please refer to the **Virtual Human as Living Partner.mp4** file in the project.

## <span style="font-size: 20px; font-weight: bold;">State Generation</span>  
<img src="./image/FourState.jpg" width="500"/>  
The system matches the timeslider and marker to generate the virtual human's states at different time periods.

## <span style="font-size: 20px; font-weight: bold;">Collision Warning</span>  
<img src="./image/Collision.jpg" width="500"/>  
When the virtual human collides with the marked object, the system displays a screen prompt and optionally plays a sound alert.

## <span style="font-size: 20px; font-weight: bold;">Communication and Animation</span>  
<img src="./image/Commu&Rig.jpg" width="500"/>  
Voice communication with the virtual human is captured via the microphone, while the animation controller adjusts the head and spine directions for eye contact.

## <span style="font-size: 20px; font-weight: bold;">Smooth Entry and Exit</span>  
<img src="./image/SmoothMove.jpg" width="500"/>  
Smooth transitions in and out of the scene.
