import asyncio
import os
import edge_tts

async def generate_test():
    os.makedirs("Assets/Resources/GuidanceAudio", exist_ok=True)
    text = "Welcome to the Bergen-Belsen Memorial AR Experience. I am your guide throughout this historic site. Select your preferred mode using the buttons above and tap Start Experience. You can also change modes at any time from the sidebar menu."
    voice = "en-US-AndrewNeural" # Warm, Natural Male Voice
    output_file = "Assets/Resources/GuidanceAudio/WELCOME_EN.mp3"
    
    communicate = edge_tts.Communicate(text, voice)
    await communicate.save(output_file)
    print(f"Successfully generated male voice audio: {output_file}")

if __name__ == "__main__":
    asyncio.run(generate_test())
