fn main() {
    // Link to vcpkg OpenCV libraries
    // Use release libraries for both debug and release builds
    // This is necessary because vcpkg's debug build may be incomplete
    println!("cargo:rustc-link-search=native=c:\\vcpkg\\installed\\x64-windows\\lib");
    println!("cargo:rustc-link-lib=opencv_core4");
    println!("cargo:rustc-link-lib=opencv_videoio4");
    println!("cargo:rustc-link-lib=opencv_imgcodecs4");
    println!("cargo:rustc-link-lib=opencv_imgproc4");
    println!("cargo:rustc-link-lib=opencv_highgui4");
    println!("cargo:rustc-link-lib=opencv_dnn4");

    // Copy OpenCV DLLs to the target directory
    copy_opencv_dlls();
}

fn copy_opencv_dlls() {
    let out_dir = std::env::var("OUT_DIR").unwrap();
    let profile = std::env::var("PROFILE").unwrap();

    // Get the target directory (e.g., target/debug or target/release)
    let target_dir = std::path::PathBuf::from(out_dir)
        .ancestors()
        .nth(3)  // Go up 4 levels: build/<out>/../..
        .expect("Cannot find target directory")
        .join(&profile);

    let vcpkg_bin = std::path::Path::new("c:\\vcpkg\\installed\\x64-windows\\bin");

    // List of required OpenCV DLLs
    let dlls = [
        "opencv_core4.dll",
        "opencv_videoio4.dll",
        "opencv_imgcodecs4.dll",
        "opencv_imgproc4.dll",
        "opencv_highgui4.dll",
        "opencv_dnn4.dll",
    ];

    for dll in &dlls {
        let src = vcpkg_bin.join(dll);
        let dst = target_dir.join(dll);

        if src.exists() {
            if let Err(e) = std::fs::copy(&src, &dst) {
                eprintln!("Warning: Failed to copy {}: {}", dll, e);
            }
        }
    }
}
