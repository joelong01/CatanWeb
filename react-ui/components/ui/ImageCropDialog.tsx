'use client';

import { useState, useCallback } from 'react';
import Cropper from 'react-easy-crop';
import type { Area } from 'react-easy-crop';

interface ImageCropDialogProps {
  /** Image source: data URI (preferred) or blob URL */
  imageSrc: string;
  /** Called with the cropped image blob */
  onCrop: (blob: Blob) => void;
  /** Called when the user cancels */
  onCancel: () => void;
}

/**
 * Crop a rectangular area from an image and return it as a 256x256 JPEG Blob.
 * Uses fetch + createImageBitmap to avoid Image constructor CORS/taint issues.
 */
async function getCroppedBlob(imageSrc: string, crop: Area): Promise<Blob> {
  const response = await fetch(imageSrc);
  const imageBlob = await response.blob();
  const bitmap = await createImageBitmap(imageBlob);

  const canvas = document.createElement('canvas');
  const size = 256;
  canvas.width = size;
  canvas.height = size;

  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas not supported');

  ctx.drawImage(bitmap, crop.x, crop.y, crop.width, crop.height, 0, 0, size, size);
  bitmap.close();

  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (blob) resolve(blob);
        else reject(new Error('Failed to create blob'));
      },
      'image/jpeg',
      0.9,
    );
  });
}

/**
 * Inline image cropper for avatar selection.
 * Renders in a fixed-size box with zoom slider.
 */
export function ImageCropDialog({
  imageSrc,
  onCrop,
  onCancel,
}: ImageCropDialogProps): React.ReactElement {
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedArea, setCroppedArea] = useState<Area | null>(null);
  const [saving, setSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [imageLoaded, setImageLoaded] = useState(false);

  const onCropComplete = useCallback((_: Area, croppedAreaPixels: Area) => {
    setCroppedArea(croppedAreaPixels);
  }, []);

  const handleApply = async () => {
    if (!croppedArea) return;
    setSaving(true);
    setErrorMsg(null);
    try {
      const blob = await getCroppedBlob(imageSrc, croppedArea);
      onCrop(blob);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Unknown error';
      setErrorMsg(`Crop failed: ${msg}`);
    }
    setSaving(false);
  };

  return (
    <div className="rounded-lg border border-gray-600 overflow-hidden bg-gray-900">
      {/* Error banner */}
      {errorMsg && (
        <div className="px-3 py-2 text-xs text-red-300 bg-red-900/40 border-b border-red-700">
          {errorMsg}
        </div>
      )}

      {/* Crop area */}
      <div className="relative w-full" style={{ height: 280 }}>
        <Cropper
          image={imageSrc}
          crop={crop}
          zoom={zoom}
          aspect={1}
          cropShape="round"
          showGrid={false}
          onCropChange={setCrop}
          onZoomChange={setZoom}
          onCropComplete={onCropComplete}
          onMediaLoaded={() => setImageLoaded(true)}
        />
      </div>

      {/* Controls */}
      <div className="flex items-center gap-3 px-3 py-2 border-t border-gray-700">
        <span className="text-xs text-gray-400 shrink-0">Zoom</span>
        <input
          type="range"
          min={1}
          max={5}
          step={0.1}
          value={zoom}
          onChange={(e) => setZoom(Number(e.target.value))}
          className="flex-1 accent-blue-500"
        />
      </div>

      {/* Buttons */}
      <div className="flex justify-end gap-2 px-3 py-2 border-t border-gray-700">
        <button
          onClick={onCancel}
          className="px-3 py-1.5 rounded text-sm text-gray-300 hover:bg-gray-700 transition-colors"
        >
          Cancel
        </button>
        <button
          onClick={handleApply}
          disabled={saving || !imageLoaded}
          className="px-3 py-1.5 rounded text-sm bg-blue-600 text-white hover:bg-blue-500 transition-colors disabled:opacity-50"
        >
          {saving ? 'Applying...' : 'Apply'}
        </button>
      </div>
    </div>
  );
}
