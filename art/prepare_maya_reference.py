"""Extract texture-alignment landmarks offline; Unity has no ML dependency.

Run in a separate Python environment with mediapipe==0.10.21, numpy and Pillow.
"""
from pathlib import Path
import json
import mediapipe as mp
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
texture = root / 'game/Assets/Art/Maya/Textures/MayaFace.png'
image = np.asarray(Image.open(texture).convert('RGB'))
with mp.solutions.face_mesh.FaceMesh(static_image_mode=True, max_num_faces=1, refine_landmarks=True) as mesh:
    result = mesh.process(image)
    if not result.multi_face_landmarks:
        raise RuntimeError('No face landmarks found in the Maya texture.')
    points = result.multi_face_landmarks[0].landmark
    output = {'width': image.shape[1], 'height': image.shape[0],
              'landmarks': [[p.x, p.y, p.z] for p in points]}
    (root / 'art/sources/maya/face-landmarks.json').write_text(json.dumps(output, indent=2) + '\n')
