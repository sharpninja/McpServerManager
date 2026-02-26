Porcupine custom keyword assets for Android

Place your Picovoice Console-generated Android `.ppn` files in this folder using these exact file names:

- `hey_tracker_android.ppn`
- `okay_tracker_android.ppn`
- `hello_tracker_android.ppn`

Notes:

- These correspond to the current user-selectable wake phrase catalog in `AndroidWakeWordCatalog`.
- The app also requires a Picovoice `AccessKey` (for example via Android app metadata key `PICOVOICE_ACCESS_KEY`).
- The current Porcupine NuGet package does not bundle Android native binaries, so you must also package `libpv_porcupine.so` for your target ABIs before wake monitoring will work on-device.
