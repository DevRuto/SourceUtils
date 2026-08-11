/// <reference path="../js/facepunch.webgame.d.ts"/>

namespace SourceUtils {
    interface IUrlWrapper {
        $url: string;
    }

    interface IConfig {
        urlPrefix?: string;
    }

    /**
     * Resolves the "$url" wrapper objects the server writes for every exported URL
     * (see UrlConverter.WriteJson) against the deployment's URL prefix, fetched once
     * from config.json.
     *
     * Every URL in this system originates from one of two places: a "$url" wrapper inside
     * a JSON response, or - just once - the literal string embedded in index.html. Both are
     * resolved to their final, prefixed form as early as possible (here), so everything
     * downstream (page.url passed to a follow-up fetch, element.url assigned to an Image,
     * etc.) already holds a ready-to-use absolute URL and never needs to know about prefixes.
     *
     * The "$url" side of that is done by patching Facepunch.Http.getJson itself, rather than
     * requiring every caller to opt in: some URLs (e.g. the lightmap and skybox textures) are
     * followed up by the vendored engine's own internal fetches, which have no notion of a
     * URL prefix or the "$url" wrapper. Patching the shared fetch function means every
     * response - ours and the engine's - gets unwrapped in the same place.
     */
    export class Config {
        private static configUrl = "config.json";
        private static prefix = "";
        private static state: "idle" | "loading" | "loaded" = "idle";
        private static pending: (() => void)[] = [];
        private static originalGetJson: typeof Facepunch.Http.getJson;

        static init(configUrl: string): void {
            if (configUrl != null) Config.configUrl = configUrl;
            Config.patch();
        }

        private static resolve(url: string): string {
            return url != null && url.charAt(0) === "/" ? Config.prefix + url : url;
        }

        private static isUrlWrapper(value: any): value is IUrlWrapper {
            return value != null && typeof value === "object" && typeof value.$url === "string";
        }

        private static resolveUrlsIn(container: any): void {
            if (container == null || typeof container !== "object") return;

            if (Array.isArray(container)) {
                for (let i = 0; i < container.length; ++i) {
                    const value = container[i];
                    if (Config.isUrlWrapper(value)) container[i] = Config.resolve(value.$url);
                    else if (typeof value === "object") Config.resolveUrlsIn(value);
                }
                return;
            }

            for (const key of Object.keys(container)) {
                const value = container[key];
                if (Config.isUrlWrapper(value)) container[key] = Config.resolve(value.$url);
                else if (typeof value === "object") Config.resolveUrlsIn(value);
            }
        }

        private static ensureLoaded(callback: () => void): void {
            if (Config.state === "loaded") {
                callback();
                return;
            }

            Config.pending.push(callback);
            if (Config.state === "loading") return;

            Config.state = "loading";

            const onDone = (info: IConfig) => {
                Config.prefix = (info && info.urlPrefix) || "";
                Config.state = "loaded";

                const callbacks = Config.pending;
                Config.pending = [];
                for (const cb of callbacks) cb();
            };

            // config.json's own URL is page-relative, not root-relative (see Index.cs), so it
            // never needs resolving; it goes through originalGetJson directly to avoid
            // ensureLoaded waiting on itself.
            Config.originalGetJson<IConfig>(Config.configUrl, onDone, error => {
                console.warn("Failed to load config.json, assuming no URL prefix.", error);
                onDone(null);
            });
        }

        private static patch(): void {
            if (Config.originalGetJson != null) return;

            Config.originalGetJson = Facepunch.Http.getJson;

            // Every URL passed in here is assumed already resolved (see class doc) - this
            // only resolves the "$url" wrappers found in the response.
            Facepunch.Http.getJson = <TResponse>(url: string,
                success: (response: TResponse) => void,
                failure?: (error: any) => void,
                progress?: (loaded: number, total: number) => void) => {
                Config.ensureLoaded(() => {
                    Config.originalGetJson<TResponse>(url, value => {
                        Config.resolveUrlsIn(value);
                        success(value);
                    }, failure, progress);
                });
            };
        }

        /**
         * For the one URL in the system that isn't already resolved: the literal
         * mapIndexJson string embedded in index.html. Everything else should just use
         * Facepunch.Http.getJson (patched above) directly.
         */
        static getJson<T>(url: string, onLoad: (value: T) => void,
            onError?: (error: any) => void,
            onProgress?: (loaded: number, total: number) => void): void {
            Config.ensureLoaded(() => {
                Facepunch.Http.getJson<T>(Config.resolve(url), onLoad, onError, onProgress);
            });
        }
    }
}
