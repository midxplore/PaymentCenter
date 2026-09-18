const radius = 6378245.0; // 长半轴
const ee = 0.006693421622965943; // 扁率
const eSquared = ee * ee;
/**
 * WGS84转GCJ02(高德，腾讯)
 * @param lng
 * @param lat
 * @returns
 */
export function wgs84ToGcj02(lng: number, lat: number) {
	let dLat = transformLat(lng - 105.0, lat - 35.0);
	let dLng = transformLng(lng - 105.0, lat - 35.0);
	let radLat = (lat / 180.0) * Math.PI;
	let magic = Math.sin(radLat);
	magic = 1 - ee * magic * magic;
	let sqrtMagic = Math.sqrt(magic);
	dLat = (dLat * 180.0) / (((radius * (1 - eSquared)) / (magic * sqrtMagic)) * Math.PI);
	dLng = (dLng * 180.0) / ((radius / sqrtMagic) * Math.cos(radLat) * Math.PI);
	return {
		lng: lng + dLng,
		lat: lat + dLat,
	};
}

/**
 * GCJ02转BD09(百度)
 * @param lng
 * @param lat
 * @returns
 */
export function gcj02ToBd09(lng: number, lat: number) {
	const z = Math.sqrt(lng * lng + lat * lat) + 0.00002 * Math.sin(lat * Math.PI);
	const theta = Math.atan2(lat, lng) + 0.000003 * Math.cos(lng * Math.PI);
	const bdLng = z * Math.cos(theta) + 0.0065;
	const bdLat = z * Math.sin(theta) + 0.006;
	return {
		lng: bdLng,
		lat: bdLat,
	};
}

/**
 * WGS84转BD09（百度）
 * @param _lng
 * @param _lat
 * @returns
 */
export function wgs84ToBd09(_lng: number, _lat: number) {
	const { lng, lat } = wgs84ToGcj02(_lng, _lat);
	return gcj02ToBd09(lng, lat);
}

const transformLat = (x: number, y: number): number => {
	let ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.sqrt(Math.abs(x));
	ret += ((20.0 * Math.sin(6.0 * x * Math.PI) + 20.0 * Math.sin(2.0 * x * Math.PI)) * 2.0) / 3.0;
	ret += ((20.0 * Math.sin(y * Math.PI) + 40.0 * Math.sin((y / 3.0) * Math.PI)) * 2.0) / 3.0;
	ret += ((160.0 * Math.sin((y / 12.0) * Math.PI) + 320 * Math.sin((y * Math.PI) / 30.0)) * 2.0) / 3.0;
	return ret;
};

const transformLng = (x: number, y: number): number => {
	let ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.sqrt(Math.abs(x));
	ret += ((20.0 * Math.sin(6.0 * x * Math.PI) + 20.0 * Math.sin(2.0 * x * Math.PI)) * 2.0) / 3.0;
	ret += ((20.0 * Math.sin(x * Math.PI) + 40.0 * Math.sin((x / 3.0) * Math.PI)) * 2.0) / 3.0;
	ret += ((150.0 * Math.sin((x / 12.0) * Math.PI) + 300.0 * Math.sin((x / 30.0) * Math.PI)) * 2.0) / 3.0;
	return ret;
};
