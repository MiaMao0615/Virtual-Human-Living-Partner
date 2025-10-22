<!--
  README for the Virtual Human as Living Partner project. This document outlines the purpose of the repository, describes the core modules of the system, provides installation and usage instructions, and includes example images to illustrate the key concepts.
-->
# Virtual Human as Living Partner: Mobile Augmented Reality Companion

This repository contains the code and assets for **Virtual Human as Living Partner**, a mobile augmented‐reality (AR) system that brings a virtual human named TongTong into the user’s physical environment. Through timeline‐based state scheduling (sleep, exercise, study, eat), the system dynamically instantiates, animates and localizes the virtual human in real space. The goal is to transform the agent into a living partner that provides continuous presence, context‑aware behavior and interactive companionship on a handheld device.


<video width="640" height="360" controls>
  <source src="https://github.com/MiaMao615/Virtual-Human-Living-Partner/raw/main/Virtual%20Human%20as%20Living%20Partner.mp4" type="video/mp4">
</video>


## Table of Contents

1. [Project Overview](#project-overview)
2. [System Architecture](#system-architecture)
3. [Daily Activity States](#daily-activity-states)
4. [Installation](#installation)

## Project Overview

**Virtual Human as Living Partner** demonstrates how AR can support persistent human‑AI coexistence. Instead of placing the agent on a 2D screen, the system uses the phone’s camera to register markers in the environment and anchor TongTong at specific locations. The user interacts with TongTong through gaze, speech and physical manipulation of objects. The system’s design is guided by four user‑experience dimensions—situational coherence, immersive interactivity, conversational naturalness and continuity of presence.



## System Architecture

The project is organized around four functional modules, each targeting one of the user‑experience dimensions. Summarize each module in your README and include a diagram to illustrate how they connect. The figure below (Figure 2 in the paper) shows the overall workflow. You should save this image to `docs/img/figure2.png` (for example) and embed it using the Markdown `![]()` syntax.

![System workflow](docs/img/figure2.png)

### Module A — State Generation

The state generation module governs TongTong’s instantiation and orchestration. It maps the time of day to one of four semantic states—study, meal, sleep and exercise—and continuously outputs the active state label. Using markers in the physical environment, the module searches for a spatial waypoint pair (Start, Target) that matches the current state; when constraints are satisfied, TongTong enters from a fallback position and follows the path from Start to Target. If no matching waypoint is found, TongTong remains off‑screen to avoid abrupt appearances. Describe how your code implements this logic (e.g., marker recognition, state machine, path planning) and point readers to the relevant files or scripts.

### Module B — Collision Warning

The collision warning module binds physical objects to virtual proxies by recognizing markers and tracking their poses. TongTong is equipped with trigger colliders and a rigid body to detect interactions; when the virtual human collides with a tagged object, the system displays an on‑screen prompt and optionally plays a sound cue. Explain how this feedback loop is implemented in Unity (e.g., using colliders, collision layers and UI elements) and how developers can add new objects.

### Module C — Dialogue Rigging

The dialogue rigging module connects user input to a large language model (LLM) and synchronizes the virtual human’s motion. It supports multimodal inputs such as speech (captured by a microphone), voice buttons and text fields. Inputs are sent through a communication layer to an LLM service, and responses are returned in real time. A dialogue state machine ensures multi‑turn coherence; when a response arrives, the animation rigging controller adjusts TongTong’s head and spine orientation to maintain eye contact. Provide details on how to configure the API key for your language model and how to extend the dialogue logic.

### Module D — Timeline Motion

The timeline motion module handles TongTong’s entrances and exits. When users change the time state using a timeline slider, the module emits a new temporal label and searches for matching spatial waypoints. If both the start and target waypoints are found, TongTong plays an entrance animation along the path; otherwise she retreats to the fallback position and plays an exit animation. Explain how you implement these animations (e.g., Unity animator controller and timeline component) and how to customise the timing.

## Daily Activity States

The system simulates a full day in the life of TongTong using four states:

| State | Description | Example Image |
|-------|-------------|---------------|
| Sleep | TongTong rests in a virtual bed, anchored to a real sleeping area. | ![Sleep](docs/img/sleep.png) |
| Exercise | She performs stretching or jumping in an exercise zone. | ![Exercise](docs/img/exercise.png) |
| Eat | TongTong sits at a dining table and eats or drinks. | ![Eat](docs/img/eat.png) |
| Study | She sits at a desk and reads or works. | ![Study](docs/img/study.png) |

These screenshots (adapted from Figure 3 of the paper) illustrate the appearance of each state and should be stored in your repository under `docs/img/figure3_*.png`. You can generate them by running the app and taking screenshots or by cropping them from the paper as shown in this example.

## Installation

Provide step‑by‑step instructions for building and running the project. At a minimum, your installation section should include:

### Prerequisites

- Unity Editor (e.g., version 2022.3 LTS)
- Vuforia Engine package for marker tracking
- Python (optional) for the local Whisper speech server
- A smartphone that supports AR (Android or iOS) with a camera and gyroscope
- Internet access to call the external LLM service (e.g., Kimi or OpenAI)

### Cloning the repository

```sh
git clone https://github.com/your‑username/virtual‑human‑living‑partner.git
cd virtual‑human‑living‑partner
